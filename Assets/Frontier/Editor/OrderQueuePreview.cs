using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class OrderQueuePreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Private).GetValue(value);
        private static void Invoke(object value,string name)=>value.GetType().GetMethod(name,Private).Invoke(value,null);

        [MenuItem("Frontier/Preview and check unit order plans")]
        public static void Render()
        {
            const string output="TestResults/OrderQueue";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Order plan session");
            var boardObject=new GameObject("Order plan board");var console=new GameObject("Order plan console");
            var previousBounds=Shader.GetGlobalVector("_FrontierBounds");var previousMatrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            int checks=0,captures=0;string language=Localization.Preference;Localization.SetPreference("en");
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.LoadMap("training");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;board.Viewer=preview.camera;Invoke(board,"Start");preview.AddSingleGO(boardObject);
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                preview.camera.transform.position=new Vector3(0,1.7f,-2);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Invoke(panel,"Start");
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Panel=panel;input.Quest=quest;
                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();
                    var workers=session.View.entities.Where(e=>e.owner==0&&!session.Catalog[e.definition].building).OrderBy(e=>e.id).Take(2).ToArray();
                    var hq=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                    session.Select(workers[0].id,false);
                    Check(session.Send(CommandKind.Move,position:new Point(22,14)),"Single MOVE accepted without a queue");Refresh();
                    Check(Plan().gameObject.activeInHierarchy&&PlanMesh().vertexCount>0&&OrderQueueVisuals.Representative(session)==null,
                        "A single active order draws its line even with no queued plan");
                    Check(PlanMesh().colors.Any(c=>c.b>.8f&&c.r<.4f),"An active Move endpoint is cyan");CaptureBoard(faction+"-single-move");
                    session.Select(workers[1].id,false);Check(session.Send(CommandKind.AttackMove,position:new Point(25,22)),"Second unit receives its own Attack Move");
                    session.Selection.Add(workers[0].id);Refresh();
                    Check(PlanMesh().colors.Any(c=>c.b>.8f&&c.r<.4f)&&PlanMesh().colors.Any(c=>c.r>.9f&&c.g<.25f&&c.b<.3f),
                        "Each selected unit shows its own current order color without any queue");
                    int groupVertices=PlanMesh().vertexCount;CaptureBoard(faction+"-mixed-active-orders");
                    session.Selection.Remove(workers[1].id);Invoke(board,"LateUpdate");
                    Check(PlanMesh().vertexCount<groupVertices&&!PlanMesh().colors.Any(c=>c.r>.9f&&c.g<.25f&&c.b<.3f),
                        "Changing selection invalidates all active-unit lines even in the same simulation view");
                    Check(session.Send(CommandKind.Move,position:new Point(22,14)),"Initial MOVE accepted");
                    Check(session.Send(CommandKind.Move,position:new Point(25,22),append:true),"Second waypoint appended");
                    Check(session.Send(CommandKind.AttackMove,position:new Point(15,24),append:true),"Attack Move waypoint appended");Refresh();
                    Check(Text("orderPlanCount")=="3 / 11","HUD counts the current order and pending orders beside its title");
                    Check(Field<Text[]>(panel,"orderNames")[0].text=="Move"&&Field<Text[]>(panel,"orderNames")[2].text=="Attack Move","HUD cards show execution order including the active action");
                    Check(Plan().gameObject.activeInHierarchy&&PlanMesh().vertexCount>0,"A selected own unit draws its queued destinations");
                    Check(Plan().GetComponentsInChildren<Collider>(true).Length==0,"Order waypoints do not intercept selection rays");
                    Check(Plan().GetComponent<Renderer>().sharedMaterial.shader.name=="Frontier/Board","Order waypoints use board clipping");
                    Check(PlanMesh().vertices.All(v=>Mathf.Abs(v.y-.025f)<.001f),"Flat terrain creates only flat overlay geometry");
                    Check(PlanMesh().colors.Any(c=>c.b>.8f&&c.r<.4f)&&PlanMesh().colors.Any(c=>c.r>.9f&&c.g<.25f&&c.b<.3f),"Move waypoints are cyan and attack waypoints are red");
                    CaptureBoard(faction+"-waypoints");CapturePanel(faction+"-orders");

                    var currentUnit=session.Match.Entities[workers[0].id];
                    var fixedSize=Field<RectTransform>(panel,"root").sizeDelta;
                    var fixedSection=Field<RectTransform>(panel,"orderPlanStrip").sizeDelta;
                    var cards=Field<Image[]>(panel,"orderCards");
                    Check(cards.Length==12&&cards.Select(c=>c.rectTransform.anchoredPosition.y).Distinct().Count()==2,
                        "Orders reserve exactly two rows including the permanent cancel-all card");
                    var last=currentUnit.pendingOrders[1].sourceSequence;
                    ClickOrder(1);Check(currentUnit.pendingOrders.Count==1&&currentUnit.pendingOrders[0].sourceSequence==last,
                        "Clicking a pending card removes only its order");
                    ClickOrder(0);Check(currentUnit.activeOrderSequence==last&&currentUnit.pendingOrders.Count==0,
                        "Clicking current advances to the next pending action immediately");
                    ClickOrder(11);Check(currentUnit.order==CommandKind.Stop&&currentUnit.pendingOrders.Count==0,"Cancel-all stops current and pending actions");
                    ClickOrder(0);ClickOrder(11);Check(currentUnit.order==CommandKind.Stop,"Empty order slots and empty cancel-all are inert");
                    session.Send(CommandKind.Move,position:new Point(22,14));
                    for(int n=0;n<Simulation.UnitOrderLimit-1;n++)Check(session.Send(n%2==0?CommandKind.Move:CommandKind.AttackMove,position:new Point(25,22),append:true),"Fill bounded order grid");
                    Refresh();Check(!session.Send(CommandKind.Move,position:new Point(25,22),append:true)&&currentUnit.pendingOrders.Count==10,"Actual queue rejects a twelfth order without dropping the previous ones");
                    Refresh();Localization.SetPreference("es");Canvas.ForceUpdateCanvases();
                    foreach(var card in cards)
                    {
                        var r=card.rectTransform;
                        Check(-r.anchoredPosition.y+r.rect.height<=fixedSection.y-24,"Two-row grid keeps its section's bottom padding");
                        foreach(var label in card.GetComponentsInChildren<Text>())
                            Check(label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text,label.GetGenerationSettings(label.rectTransform.rect.size))/label.pixelsPerUnit<=label.rectTransform.rect.height+.5f,
                                "Spanish order-card text fits without shrinking or clipping: "+label.text);
                    }
                    CapturePanel(faction+"-full-grid-es");
                    panel.Point(OrderRay(0),false,false);CapturePanel(faction+"-current-hover-es");
                    Check(cards[0].color==ConsoleTheme.Accent,"Current order has the same accent hover as production");
                    Localization.SetPreference("en");
                    int optionsIdentity=Field<RectTransform>(panel,"actions").GetChild(0).GetInstanceID();
                    var held=OrderRay(0);panel.Point(held,true,false);
                    session.Send(CommandKind.CancelUnitOrder,unitOrderSequence:currentUnit.activeOrderSequence);
                    session.Send(CommandKind.Move,position:new Point(25,22),append:true);Refresh();
                    Check(Field<RectTransform>(panel,"actions").GetChild(0).GetInstanceID()==optionsIdentity,"Advancing orders preserves existing option buttons and does not rebuild the menu");
                    int successor=currentUnit.activeOrderSequence;panel.Point(held,false,true);Refresh();
                    Check(currentUnit.activeOrderSequence==successor&&currentUnit.pendingOrders.Count==10,"Advancing a full queue invalidates a held click even when its length stays unchanged");
                    held=OrderRay(0);panel.Point(held,true,false);panel.CancelPointerInteraction();panel.Point(held,false,true);Refresh();
                    Check(currentUnit.activeOrderSequence==successor,"Losing pointer focus cancels a pending order-card click");
                    ClickOrder(11);Check(currentUnit.order==CommandKind.Stop&&currentUnit.pendingOrders.Count==0,"Cancel-all removes a full queue including current");
                    Check(Field<RectTransform>(panel,"root").sizeDelta==fixedSize&&Field<RectTransform>(panel,"orderPlanStrip").sizeDelta==fixedSection,
                        "Empty, full, error, hover and cancellation states preserve both section and menu dimensions");

                    session.Select(hq.id,false);Check(session.Send(CommandKind.Train,workers[0].definition),"Production starts independently of unit orders");Refresh();
                    Check(!Field<RectTransform>(panel,"orderPlanStrip").gameObject.activeSelf&&Field<RectTransform>(panel,"productionStrip").gameObject.activeSelf,"Buildings show production, not a unit order queue");
                    Check(!Plan().gameObject.activeInHierarchy,"Selecting another item hides the previous unit plan");
                    session.Selection.Add(workers[0].id);Refresh();
                    Check(Field<RectTransform>(panel,"orderPlanStrip").gameObject.activeSelf&&!Field<RectTransform>(panel,"productionStrip").gameObject.activeSelf,"Mixed building and mobile selection shows unit orders without an overlapping production strip");
                    session.Select(workers[0].id,false);session.Selection.Add(workers[1].id);
                    Check(session.Send(CommandKind.Move,position:new Point(22,14)),"Group MOVE accepted");
                    Check(session.Send(CommandKind.Move,position:new Point(25,22),append:true),"Group waypoint appended");Refresh();
                    Check(Text("orderPlanCount")=="2 / 11","Group HUD counts the representative plan including current action");
                    Check(OrderQueueVisuals.Representative(session).id==workers[0].id,"One stable lowest-ID unit represents the selected group");
                    ClickOrder(1);Check(workers.All(w=>session.Match.Entities[w.id].pendingOrders.Count==0),"Clicking a shared pending card removes that command from both selected units");
                    Check(session.Send(CommandKind.Move,position:new Point(25,22),append:true),"Restore group waypoint after targeted cancellation");Refresh();
                    session.Selection.Remove(workers[0].id);Refresh();
                    Check(OrderQueueVisuals.Representative(session).id==workers[1].id,"Removing the representative changes the displayed plan");
                    Check(session.Send(CommandKind.Stop),"Stop accepted");Refresh();
                    Check(Text("orderPlanCount")=="0 / 11"&&!Plan().gameObject.activeInHierarchy,"Stop clears queued order feedback immediately");
                    session.Select(workers[0].id,false);Refresh();
                    var selected=session.View.entities.First(e=>e.id==workers[0].id);selected.owner=1;
                    Invoke(board,"LateUpdate");Check(OrderQueueVisuals.Representative(session)==null&&!Plan().gameObject.activeInHierarchy,"Enemy data cannot become a displayed own plan even if selected");Refresh();
                    session.Selection.Clear();Refresh();Check(!Plan().gameObject.activeInHierarchy,"Empty selection clears all waypoint geometry");
                    session.Select(workers[0].id,false);Refresh();board.SetVisible(false);
                    Check(!Plan().gameObject.activeInHierarchy,"Hiding the board hides its order plan");board.SetVisible(true);Refresh();
                    Check(Plan().gameObject.activeInHierarchy,"Restoring the board restores the selected unit plan");
                    session.Match.Entities.Remove(workers[0].id);Refresh();Check(!Plan().gameObject.activeInHierarchy,"Destroyed selected unit leaves no stale plan");
                    session.LeaveMatch();Refresh();Check(!Plan().gameObject.activeInHierarchy,"Leaving the match clears order overlays");
                }
                session.StartMatch();typeof(CommandPanel).GetMethod("Show",Private).Invoke(panel,new object[]{CommandPanel.MenuPage.Guide});Refresh();
                Check(Field<Text[]>(panel,"controlGuideGestures")[4].text.Contains("double "+panel.Bindings.Name(true,"select"))
                    &&Field<Text[]>(panel,"controlGuideDescriptions")[3].text.Contains("0.5 s"),"The current Selection guide describes double selection and stationary deselection");
                typeof(CommandPanel).GetField("controlGuideTopic",Private).SetValue(panel,1);Invoke(panel,"Rebuild");Refresh();
                Check(Field<Text[]>(panel,"controlGuideGestures")[2].text.Contains("Double "+panel.Bindings.Name(true,"order"))
                    &&Field<Text[]>(panel,"controlGuideDescriptions")[2].text.Contains("immediately"),"The Orders guide distinguishes the immediate double press from menu targeting");
                CapturePanel("controls");
                Debug.Log($"FRONTIER_ORDER_QUEUE_PREVIEW passed: {checks} checks, {captures} views; no player build");

                void Refresh()
                {
                    if(session.Running)typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));
                    typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Invoke(panel,"LateUpdate");Invoke(board,"LateUpdate");
                }
                Ray OrderRay(int index){var r=Field<Image[]>(panel,"orderCards")[index].rectTransform;return new Ray(r.TransformPoint(r.rect.center)-r.forward,r.forward);}
                void ClickOrder(int index)
                {
                    Refresh();var ray=OrderRay(index);bool a=index%2==1;
                    bool Frame(bool down)=>(bool)typeof(CommanderInput).GetMethod("RoutePanel",Private).Invoke(input,new object[]{ray,!a&&down,!a&&!down,!a&&down,a&&down,a&&!down,a&&down,0f});
                    Check(Frame(true)&&Frame(false),"Order-card Trigger / A click stays in the menu");Refresh();
                }
                string Text(string name)=>Field<Text>(panel,name).text;
                Transform Plan()=>board.Content.Find("Unit order waypoints");
                Mesh PlanMesh()=>Plan().GetComponent<MeshFilter>().sharedMesh;
                void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("ORDER_PLAN_PASS: "+message);}
                void CapturePanel(string name)
                {
                    boardObject.SetActive(false);HarvestingPreview.SavePanel(panel,output+"/"+name+".png",preview);boardObject.SetActive(true);captures++;Refresh();
                }
                void CaptureBoard(string name)
                {
                    preview.camera.orthographic=true;preview.camera.orthographicSize=.34f;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    var center=board.Content.TransformPoint(new Vector3(20,0,19));preview.camera.transform.position=center+new Vector3(0,.8f,-.35f);preview.camera.transform.LookAt(center);
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                    preview.BeginStaticPreview(new Rect(0,0,1200,900));preview.Render(true);var capture=preview.EndStaticPreview();
                    File.WriteAllBytes(output+"/"+name+".png",capture.EncodeToPNG());Object.DestroyImmediate(capture);captures++;
                }
            }
            finally
            {
                Localization.SetPreference(language);Object.DestroyImmediate(console);Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",previousBounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",previousMatrix);
            }
        }
    }
}
