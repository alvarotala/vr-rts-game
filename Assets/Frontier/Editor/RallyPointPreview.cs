using TMPro;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class RallyPointPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Private).GetValue(value);
        private static void Invoke(object value,string name)=>value.GetType().GetMethod(name,Private).Invoke(value,null);

        [MenuItem("Frontier/Preview and check rally points")]
        public static void Render()
        {
            const string output="TestResults/RallyPoint";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Rally point session");
            var boardObject=new GameObject("Rally point board");var console=new GameObject("Rally point console");
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
                    session.Faction=faction;session.StartMatch();session.Match.Players[0].minerals=5000;session.Match.Players[0].catalyst=5000;
                    var hq=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                    var worker=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                    var barracks=AddBuilding("barracks",1001,new Point(20,8));
                    var supply=AddBuilding("supply",1002,new Point(8,8));
                    var tech=AddBuilding("tech",1003,new Point(20,24));Refresh();
                    session.Select(hq.id,false);Refresh();
                    Check(Shown("rallyPointStrip")&&Text("rallyPointValue")=="DEFAULT · No rally point","Selected headquarters explains the default rally behavior");
                    Check(Text("rallyPointHint").Contains("A on terrain: set rally point")&&!Plan().gameObject.activeInHierarchy,"Default rally needs no map marker and describes its shortcut");
                    CapturePanel(faction+"-default");
                    Check(session.Send(CommandKind.Train,worker.definition),"Training remains available before setting a rally point");
                    int productionId=hq.queue[0].id;
                    Check(session.Context(new Point(24,14),0,0)>0,"A contextual terrain action sets a headquarters rally point");Refresh();
                    Check(hq.hasRallyPoint&&hq.queue[0].id==productionId,"Setting a rally point preserves the active production item");
                    Check(Text("rallyPointValue").Contains("SET")&&Text("rallyPointValue").Contains("24")&&Shown("productionStrip"),"Rally coordinates and production remain visible together");
                    Check(Plan().gameObject.activeInHierarchy&&PlanMesh().vertexCount>0,"Selected producer draws a rally marker");
                    Check(Plan().GetComponentsInChildren<Collider>(true).Length==0,"Rally overlays never intercept selection rays");
                    Check(Plan().GetComponent<Renderer>().sharedMaterial.shader.name=="Frontier/Board","Rally overlays use the clipped board material");
                    Check(PlanMesh().vertices.All(v=>Mathf.Abs(v.y-.025f)<.001f),"The rally line, destination and flag all lie on the map surface");
                    float footprint=session.Catalog[hq.definition].footprint;
                    Check(PlanMesh().vertices.All(v=>v.x<=hq.position.x||v.x>=hq.position.x+footprint||v.z<=hq.position.y||v.z>=hq.position.y+footprint),"The rally connection begins outside the building footprint");
                    Check(PlanMesh().colors.Any(c=>c.b>.8f&&c.r<.4f)&&PlanMesh().colors.Any(c=>c.r>.9f&&c.g>.9f&&c.b>.9f),"Rally point is cyan with a distinct white flag pole");
                    var origin=new Vector2(hq.position.x+footprint*.5f,hq.position.y+footprint*.5f);
                    var destination=new Vector2(hq.rallyPoint.x,hq.rallyPoint.y);
                    int solid=0,gap=0;
                    // Simulation snaps the requested (24,14) to the cell center
                    // (24.5,14.5): sample the accepted diagonal, away from both markers.
                    for(float t=.3f;t<.75f;t+=.01f){if(OnMesh(Vector2.Lerp(origin,destination,t)))solid++;else gap++;}
                    Check(solid>0&&gap>0,"Rally connection has both visible dashes and empty gaps");
                    Check(OnMesh(destination),"Rally destination dot matches the accepted cell center");
                    Check(PanelGap("productionStrip","rallyPointStrip")>=11&&PanelGap("rallyPointStrip","harvestingStrip")>=11,"Rally and production sections do not overlap each other or harvesting");
                    CaptureBoard(faction+"-rally");CapturePanel(faction+"-production");

                    session.Select(barracks.id,false);Check(session.Context(new Point(26,20),0,0)>0,"Barracks accepts its own independent rally point");
                    session.Select(hq.id,true);Refresh();
                    Check(OrderQueueVisuals.RallyProducers(session).Length==2&&Text("rallyPointValue")=="2 / 2 rally points set","Multiple selected producers keep their individual destinations visible");
                    Check(Shown("productionStrip")&&Shown("rallyPointStrip")&&Text("productionTitle")=="GROUP PRODUCTION"
                        &&Text("productionCount")=="1 / 10"&&hq.queue[0].id==productionId&&barracks.queue.Count==0
                        &&PanelGap("productionStrip","rallyPointStrip")>=11,"Multiple producers keep rally separate from their production summary and preserve individual queues");
                    CaptureBoard(faction+"-multiple");

                    session.Select(tech.id,false);
                    Check(session.Context(new Point(26,20),0,0)>0&&session.Send(CommandKind.Research,"attack")&&session.Send(CommandKind.Train,session.Catalog.Role(faction,"siege").id),"Technology building combines rally, research and training");Refresh();
                    var actions=Field<RectTransform>(panel,"actions");
                    Check(actions.GetComponentsInChildren<Text>().Where(t=>t.text=="Clear rally point"||t.text=="Clear Rally Point").Any(),"Technology building exposes its clear-rally action");
                    Check(actions.Cast<Transform>().OfType<RectTransform>().All(r=>-r.anchoredPosition.y+r.sizeDelta.y<=actions.sizeDelta.y+.01f),"Technology actions fit inside their card with research, production and rally controls");
                    Check(Text("productionName")=="Firepower I"&&Shown("rallyPointStrip"),"Research queue and rally remain separate readable sections");CapturePanel(faction+"-technology");

                    session.Select(worker.id,false);Check(session.Send(CommandKind.Move,position:new Point(22,18)),"Unit movement remains available");
                    Check(session.Send(CommandKind.AttackMove,position:new Point(25,23),append:true),"Unit attack waypoint is queued");Refresh();
                    int unitVertices=PlanMesh().vertexCount;session.Select(hq.id,true);Refresh();
                    Check(Shown("orderPlanStrip")&&Shown("rallyPointStrip")&&PlanMesh().vertexCount>unitVertices,"Mixed selection displays the unit plan and producer rally simultaneously");
                    Check(PlanMesh().colors.Any(c=>c.r>.9f&&c.g<.25f)&&PlanMesh().colors.Any(c=>c.b>.8f&&c.r<.4f),"Unit attack waypoints retain red beside the cyan rally flag");
                    Check(Text("rallyPointHint").Contains("Select only production buildings")&&PanelGap("orderPlanStrip","rallyPointStrip")>=11,"Mixed selection explains the producer-only shortcut and avoids overlapping sections");
                    CaptureBoard(faction+"-mixed-map");CapturePanel(faction+"-mixed");

                    session.Select(hq.id,false);Check(session.ClearRallyPoint(),"Clear rally point action is accepted");Refresh();
                    Check(!Plan().gameObject.activeInHierarchy&&Text("rallyPointValue").Contains("DEFAULT"),"Clearing the rally removes its map flag immediately");
                    session.Select(barracks.id,false);Refresh();board.SetVisible(false);
                    Check(!Plan().gameObject.activeInHierarchy,"Hidden board hides its rally overlay");board.SetVisible(true);Refresh();
                    Check(Plan().gameObject.activeInHierarchy,"Restored board restores the rally overlay");
                    session.View.entities.First(e=>e.id==barracks.id).owner=1;Invoke(board,"LateUpdate");
                    Check(!Plan().gameObject.activeInHierarchy,"Enemy producer data never draws a selected own rally");Refresh();
                    barracks.complete=false;Refresh();Check(!Shown("rallyPointStrip")&&!Plan().gameObject.activeInHierarchy,"Unfinished producers do not expose the completed-building shortcut");barracks.complete=true;
                    session.Select(supply.id,false);Refresh();Check(!Shown("rallyPointStrip"),"Non-producing buildings have no rally point section");
                    session.Select(barracks.id,false);Refresh();session.Match.Entities.Remove(barracks.id);Refresh();
                    Check(!Plan().gameObject.activeInHierarchy&&!Shown("rallyPointStrip"),"Destroyed producer leaves no stale marker or rally section");
                    session.Select(hq.id,false);Check(session.Context(new Point(24,14),0,0)>0,"Rally can be set again after clearing");Refresh();
                    session.Selection.Clear();Refresh();Check(!Plan().gameObject.activeInHierarchy,"Deselecting producers clears rally overlays");
                    session.Select(hq.id,false);Refresh();session.LeaveMatch();Refresh();Check(!Plan().gameObject.activeInHierarchy,"Leaving the match clears rally overlays");

                    EntityState AddBuilding(string role,int id,Point position)
                    {
                        var type=session.Catalog.Role(faction,role);var entity=new EntityState{id=id,owner=0,definition=type.id,health=type.health,position=position};
                        session.Match.Entities.Add(id,entity);session.Match.Map.Occupy(position,type.footprint,id);return entity;
                    }
                }
                Debug.Log($"FRONTIER_RALLY_POINT_PREVIEW passed: {checks} checks, {captures} views; no player build");

                void Refresh()
                {
                    if(session.Running)typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));
                    typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Invoke(panel,"LateUpdate");Invoke(board,"LateUpdate");
                }
                string Text(string name)=>Field<Text>(panel,name).text;
                bool Shown(string name)=>Field<RectTransform>(panel,name).gameObject.activeInHierarchy;
                float PanelGap(string upper,string lower)
                {
                    var a=Field<RectTransform>(panel,upper);var b=Field<RectTransform>(panel,lower);
                    return -b.anchoredPosition.y-(-a.anchoredPosition.y+a.sizeDelta.y);
                }
                Transform Plan()=>board.Content.Find("Unit order waypoints");
                Mesh PlanMesh()=>Plan().GetComponent<MeshFilter>().sharedMesh;
                bool OnMesh(Vector2 point)
                {
                    var vertices=PlanMesh().vertices;var indices=PlanMesh().triangles;
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        Vector2 A(Vector3 value)=>new Vector2(value.x,value.z);
                        var a=A(vertices[indices[i]]);var b=A(vertices[indices[i+1]]);var c=A(vertices[indices[i+2]]);
                        float Cross(Vector2 first,Vector2 second)=>first.x*second.y-first.y*second.x;
                        float ab=Cross(b-a,point-a),bc=Cross(c-b,point-b),ca=Cross(a-c,point-c);
                        if(ab>=-.00001f&&bc>=-.00001f&&ca>=-.00001f||ab<=.00001f&&bc<=.00001f&&ca<=.00001f)return true;
                    }
                    return false;
                }
                void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("RALLY_POINT_PASS: "+message);}
                void CapturePanel(string name)
                {
                    boardObject.SetActive(false);HarvestingPreview.SavePanel(panel,output+"/"+name+".png",preview);boardObject.SetActive(true);captures++;Refresh();
                }
                void CaptureBoard(string name)
                {
                    preview.camera.orthographic=true;preview.camera.orthographicSize=.36f;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    var center=board.Content.TransformPoint(new Vector3(20,0,17));preview.camera.transform.position=center+new Vector3(0,.8f,-.35f);preview.camera.transform.LookAt(center);
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
