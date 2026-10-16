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
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.LoadMap("training");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;board.Viewer=preview.camera;Invoke(board,"Start");preview.AddSingleGO(boardObject);
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                preview.camera.transform.position=new Vector3(0,1.7f,-2);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Invoke(panel,"Start");
                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();
                    var workers=session.View.entities.Where(e=>e.owner==0&&!session.Catalog[e.definition].building).OrderBy(e=>e.id).Take(2).ToArray();
                    var hq=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                    session.Select(workers[0].id,false);
                    Check(session.Send(CommandKind.Move,position:new Point(22,14)),"Initial MOVE accepted");
                    Check(session.Send(CommandKind.Move,position:new Point(25,22),append:true),"Second waypoint appended");
                    Check(session.Send(CommandKind.AttackMove,position:new Point(15,24),append:true),"Attack Move waypoint appended");Refresh();
                    Check(Text("orderPlanCount")=="2 queued orders","HUD distinguishes queued orders from the active order");
                    Check(Text("orderPlanSteps").Contains("1. Move")&&Text("orderPlanSteps").Contains("3. Attack Move"),"HUD lists upcoming order types in execution order");
                    Check(Plan().gameObject.activeInHierarchy&&PlanMesh().vertexCount>0,"A selected own unit draws its queued destinations");
                    Check(Plan().GetComponentsInChildren<Collider>(true).Length==0,"Order waypoints do not intercept selection rays");
                    Check(Plan().GetComponent<Renderer>().sharedMaterial.shader.name=="Frontier/Board","Order waypoints use board clipping");
                    Check(PlanMesh().vertices.All(v=>Mathf.Abs(v.y-.025f)<.001f),"Flat terrain creates only flat overlay geometry");
                    Check(PlanMesh().colors.Any(c=>c.b>.8f&&c.r<.4f)&&PlanMesh().colors.Any(c=>c.r>.9f&&c.g<.25f&&c.b<.3f),"Move waypoints are cyan and attack waypoints are red");
                    CaptureBoard(faction+"-waypoints");CapturePanel(faction+"-orders");

                    session.Select(hq.id,false);Check(session.Send(CommandKind.Train,workers[0].definition),"Production starts independently of unit orders");Refresh();
                    Check(!Field<RectTransform>(panel,"orderPlanStrip").gameObject.activeSelf&&Field<RectTransform>(panel,"productionStrip").gameObject.activeSelf,"Buildings show production, not a unit order queue");
                    Check(!Plan().gameObject.activeInHierarchy,"Selecting another item hides the previous unit plan");
                    session.Selection.Add(workers[0].id);Refresh();
                    Check(Field<RectTransform>(panel,"orderPlanStrip").gameObject.activeSelf&&!Field<RectTransform>(panel,"productionStrip").gameObject.activeSelf,"Mixed building and mobile selection shows unit orders without an overlapping production strip");
                    session.Select(workers[0].id,false);session.Selection.Add(workers[1].id);
                    Check(session.Send(CommandKind.Move,position:new Point(22,14)),"Group MOVE accepted");
                    Check(session.Send(CommandKind.Move,position:new Point(25,22),append:true),"Group waypoint appended");Refresh();
                    Check(Text("orderPlanCount")=="2 queued orders across 2 / 2 units","Group HUD identifies per-unit queue totals");
                    Check(OrderQueueVisuals.Representative(session).id==workers[0].id,"One stable lowest-ID unit represents the selected group");
                    session.Selection.Remove(workers[0].id);Refresh();
                    Check(OrderQueueVisuals.Representative(session).id==workers[1].id,"Removing the representative changes the displayed plan");
                    Check(session.Send(CommandKind.Stop),"Stop accepted");Refresh();
                    Check(Text("orderPlanCount")=="0 queued orders"&&!Plan().gameObject.activeInHierarchy,"Stop clears queued order feedback immediately");
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
                session.StartMatch();typeof(CommandPanel).GetMethod("Show",Private).Invoke(panel,new object[]{CommandPanel.MenuPage.Controls});Refresh();
                Check(Text("selection").Contains("Double trigger")&&Text("selection").Contains("Double A")&&Text("selection").Contains("Grip + hold Trigger still (0.5s): deselect pointed unit")&&!Text("selection").Contains("recall menu"),"Controller help describes double clicks and stationary grip-trigger deselection");
                CapturePanel("controls");
                Debug.Log($"FRONTIER_ORDER_QUEUE_PREVIEW passed: {checks} checks, {captures} views; no player build");

                void Refresh()
                {
                    if(session.Running)typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));
                    typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Invoke(panel,"LateUpdate");Invoke(board,"LateUpdate");
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
                Object.DestroyImmediate(console);Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",previousBounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",previousMatrix);
            }
        }
    }
}
