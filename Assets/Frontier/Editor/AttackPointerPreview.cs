using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class AttackPointerPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);

        [MenuItem("Frontier/Preview attack pointer")]
        public static void Render()
        {
            Directory.CreateDirectory("TestResults/AttackPointer");
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Attack pointer session");
            var boardObject=new GameObject("Attack pointer board");var console=new GameObject("Attack pointer console");
            var rayObject=new GameObject("Attack pointer ray");
            var rayMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Call(board,"Start");preview.AddSingleGO(boardObject);
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Call(panel,"Start");
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Quest=quest;input.Panel=panel;
                var laser=rayObject.AddComponent<LineRenderer>();laser.sharedMaterial=rayMaterial;Set(input,"laser",laser);
                var point=new Point(20.5f,12.5f);
                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();board.Width=1.2f;board.Fit();
                    var worker=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                    session.Select(worker.id,false);session.Send(CommandKind.Stop);board.Pointer=point;Refresh();
                    Check(!session.AttackPointerActive&&Normal(),"Normal pointer has cyan guides and white center: "+faction);Capture(faction+"-normal");

                    ClickMenuAttack();Refresh();
                    Check(session.AttackMoveMode&&worker.order==CommandKind.Stop&&Red(),"Menu attack arms the red pointer without issuing an order: "+faction);Capture(faction+"-armed");
                    Check(session.Context(new Point(-10,-10),0,0)==0&&session.AttackMoveMode,"Rejected destination keeps attack targeting armed: "+faction);
                    Refresh();Check(Red(),"Rejected destination keeps the cursor red: "+faction);
                    session.Pause("pointer-check",true);Refresh();
                    Check(!session.AttackPointerActive&&Ground().GetVector("_PointerArea").w==0&&Normal(),"Pause hides board targeting and clears the red ray: "+faction);
                    session.Pause("pointer-check",false);Refresh();Check(Red(),"Unpausing restores an unconfirmed attack destination: "+faction);
                    session.CancelMode();Refresh();Check(!session.AttackMoveMode&&Normal(),"Cancel returns both pointers to normal: "+faction);

                    ClickMenuAttack();
                    var ray=new Ray(board.Content.TransformPoint(new Vector3(point.x,0,point.y))+Vector3.up,Vector3.down);
                    Call(input,"PointBoard",ray,true,false,false,false);Call(input,"PointBoard",ray,false,true,false,false);Refresh();
                    Check(!session.AttackMoveMode&&worker.order==CommandKind.AttackMove&&session.Selection.Contains(worker.id)&&Red(),
                        "Separate trigger click confirms the attack point and flashes red without changing selection: "+faction);Capture(faction+"-confirmed");
                    Set(session,"attackFeedbackUntil",0f);Refresh();
                    Check(Normal()&&worker.order==CommandKind.AttackMove,"Feedback expires without ending the unit attack order: "+faction);

                    Call(input,"ClearPointer");board.Pointer=point;session.Send(CommandKind.Stop);
                    Call(input,"OrderClick",point,0,0,false,10f);Refresh();Check(worker.order==CommandKind.Move&&Normal(),"First A remains normal movement: "+faction);
                    Call(input,"OrderClick",point,0,0,false,10.1f);Refresh();
                    Check(worker.order==CommandKind.AttackMove&&!session.AttackMoveMode&&Red(),"Double A confirms attack with a red ground mark and controller ray: "+faction);Capture(faction+"-double-a");
                    session.Send(CommandKind.Move,position:new Point(22.5f,12.5f));Refresh();Check(Normal(),"Manual movement clears attack feedback immediately: "+faction);
                    session.CancelMode();Check(session.Context(new Point(-10,-10),0,0,attack:true)==0,"Invalid double-A destination rejects: "+faction);
                    Refresh();Check(Normal(),"Rejected immediate attack does not flash accepted-order feedback: "+faction);

                    session.ChooseAttackMove();session.ChooseBuild(session.Catalog.Role(faction,"supply").id);Refresh();
                    Check(!session.AttackMoveMode&&Normal(),"Choosing construction cancels red attack targeting: "+faction);
                    session.ChooseAttackMove();session.LeaveMatch();Refresh();Check(!session.AttackPointerActive&&Normal(),"Leaving the match clears attack targeting: "+faction);
                }
                Debug.Log($"FRONTIER_ATTACK_POINTER passed: {checks} checks, {captures} views; no player build");

                void Refresh(){Call(board,"LateUpdate");Call(input,"LateUpdate");}
                Material Ground()=>board.Content.Find("Terrain").GetComponent<Renderer>().sharedMaterial;
                bool Close(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b))<.01f;
                bool Red()=>session.AttackPointerActive&&Close(Ground().GetColor("_PointerColor"),BoardView.AttackPointerColor)
                    &&Ground().GetColor("_PointerDotColor").r>.9f&&Ground().GetColor("_PointerDotColor").g<.4f&&Close(rayMaterial.color,BoardView.AttackPointerColor);
                bool Normal()=>Close(Ground().GetColor("_PointerColor"),BoardView.NormalPointerColor)&&Close(Ground().GetColor("_PointerDotColor"),Color.white)
                    &&Close(rayMaterial.color,BoardView.NormalPointerColor);
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("ATTACK_POINTER_PASS: "+message);}
                void ClickMenuAttack()
                {
                    Call(panel,"Rebuild");
                    var button=panel.GetComponentsInChildren<RectTransform>().Single(t=>t.gameObject.activeInHierarchy&&t.name=="Attack Move");
                    var center=button.TransformPoint(button.rect.center);var ray=new Ray(center-button.forward,button.forward);
                    Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Attack command can be clicked on the actual panel: "+session.Faction);
                }
                void Capture(string name)
                {
                    var center=board.Content.TransformPoint(new Vector3(point.x,board.HeightAt(point),point.y));float size=board.CellScale*3;
                    preview.camera.orthographic=true;preview.camera.orthographicSize=size;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    preview.camera.transform.position=center+board.transform.up*size*2-board.transform.forward*size*2.4f;preview.camera.transform.LookAt(center,board.transform.up);
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                    preview.BeginStaticPreview(new Rect(0,0,1000,800));preview.Render(true);var snapshot=preview.EndStaticPreview();
                    File.WriteAllBytes("TestResults/AttackPointer/"+name+".png",snapshot.EncodeToPNG());Object.DestroyImmediate(snapshot);captures++;
                }
            }
            finally
            {
                Object.DestroyImmediate(console);Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);Object.DestroyImmediate(rayObject);Object.DestroyImmediate(rayMaterial);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
            }
            QueuedInputChecks.Run();
            ActionWheelPreview.Render();
        }
    }
}
