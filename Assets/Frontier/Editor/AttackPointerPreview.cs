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
                    Check(!session.AttackMoveMode&&worker.order==CommandKind.AttackMove&&session.Selection.Contains(worker.id)&&Normal()&&Pulse(point,true),
                        "Menu confirmation leaves a red destination pulse and returns the cursor to normal: "+faction);Capture(faction+"-confirmed");
                    typeof(GameSession).GetProperty("LastCommandTime").SetValue(session,Time.unscaledTime-GameSession.CommandPulseDuration-.05f);Refresh();
                    Check(Normal()&&!session.CommandPulseActive&&worker.order==CommandKind.AttackMove,"Pulse expiry does not end the unit attack order: "+faction);

                    Call(input,"ClearPointer");board.Pointer=point;session.Send(CommandKind.Stop);
                    Call(input,"OrderClick",point,0,0,false,10f);Refresh();Check(worker.order==CommandKind.Move&&Normal()&&Pulse(point,false),"First A moves immediately and marks the destination in cyan: "+faction);Capture(faction+"-move-pulse");
                    Call(input,"OrderClick",point,0,0,false,10.1f);Refresh();
                    Check(worker.order==CommandKind.AttackMove&&!session.HasTargetingMode&&Normal()&&Pulse(point,true),"Double A issues attack immediately with a red blip and no targeting mode: "+faction);Capture(faction+"-double-a");
                    float radius=Ground().GetVector("_CommandPulse").z;
                    board.Pointer=new Point(point.x+2,point.y+2);
                    typeof(GameSession).GetProperty("LastCommandTime").SetValue(session,Time.unscaledTime-.5f);Refresh();
                    Check(Pulse(point,true)&&Ground().GetVector("_CommandPulse").z>radius,"The animated blip expands at the issued point after the cursor moves away: "+faction);Capture(faction+"-fixed-attack-pulse");
                    board.Pointer=null;Refresh();Check(Pulse(point,true),"The accepted-order blip survives pointing away from the map: "+faction);
                    session.Pause("pulse-check",true);Refresh();Check(Ground().GetVector("_CommandPulse").w==0,"Pause hides order pulses: "+faction);session.Pause("pulse-check",false);
                    board.SetVisible(false);Refresh();Check(Ground().GetVector("_CommandPulse").w==0,"Hidden boards hide order pulses: "+faction);board.SetVisible(true);
                    session.Send(CommandKind.Move,position:new Point(22.5f,12.5f));Refresh();Check(Normal()&&Pulse(new Point(22.5f,12.5f),false),"A new movement order replaces the red blip with a cyan one: "+faction);
                    int lastCommand=session.LastCommandSequence;
                    session.CancelMode();Check(session.Context(new Point(-10,-10),0,0,attack:true)==0,"Invalid double-A destination rejects: "+faction);
                    Refresh();Check(Normal()&&lastCommand==session.LastCommandSequence&&session.LastCommandKind==CommandKind.Move,"Rejected orders cannot create a success blip: "+faction);

                    CheckTouchDoubleOrder(worker);

                    session.ChooseAttackMove();session.ChooseBuild(session.Catalog.Role(faction,"supply").id);Refresh();
                    Check(!session.AttackMoveMode&&Normal(),"Choosing construction cancels red attack targeting: "+faction);
                    session.ChooseAttackMove();session.LeaveMatch();Refresh();Check(!session.AttackPointerActive&&!session.CommandPulseActive&&Normal(),"Leaving the match clears targeting and command feedback: "+faction);
                }
                Debug.Log($"FRONTIER_ATTACK_POINTER passed: {checks} checks, {captures} views; no player build");

                void Refresh(){Call(board,"LateUpdate");Call(input,"LateUpdate");}
                Material Ground()=>board.Content.Find("Terrain").GetComponent<Renderer>().sharedMaterial;
                bool Close(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b))<.01f;
                bool Red()=>session.AttackPointerActive&&Close(Ground().GetColor("_PointerColor"),BoardView.AttackPointerColor)
                    &&Ground().GetColor("_PointerDotColor").r>.9f&&Ground().GetColor("_PointerDotColor").g<.4f&&Close(rayMaterial.color,BoardView.AttackPointerColor);
                bool Normal()=>Close(Ground().GetColor("_PointerColor"),BoardView.NormalPointerColor)&&Close(Ground().GetColor("_PointerDotColor"),Color.white)
                    &&Close(rayMaterial.color,BoardView.NormalPointerColor);
                bool Pulse(Point destination,bool attack)
                {
                    var pulse=Ground().GetVector("_CommandPulse");
                    return pulse.w>0&&new Point(pulse.x,pulse.y).Distance(destination)<.01f
                        &&Close(Ground().GetColor("_CommandColor"),attack?BoardView.AttackPointerColor:BoardView.NormalPointerColor);
                }
                void CheckTouchDoubleOrder(EntityState worker)
                {
                    var previousControllers=OVRInput.controllers;var previousBindings=panel.Bindings;var previousQuestBindings=quest.Bindings;
                    var previousTracking=quest.TrackingSpace;bool previousActive=quest.Active;var previousConsole=console.transform.position;
                    var tracking=new GameObject("Synthetic order tracking");
                    var left=new OVRInput.OVRControllerLTouch();
                    var right=(OVRInput.OVRControllerBase)Activator.CreateInstance(typeof(OVRInput).GetNestedType("OVRControllerRTouch",BindingFlags.NonPublic),true);
                    var wheel=fixture.AddComponent<ActionWheel>();wheel.Session=session;wheel.Panel=panel;wheel.Quest=quest;input.Wheel=wheel;
                    OVRInput.controllers=new System.Collections.Generic.List<OVRInput.OVRControllerBase>{left,right};
                    typeof(QuestBridge).GetProperty("TrackingSpace").SetValue(quest,tracking.transform);
                    typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);console.transform.position=Vector3.one*100;
                    try
                    {
                        foreach(bool useLeft in new[]{false,true})foreach(bool remapped in new[]{false,true})
                        {
                            var bindings=new HotkeyBindings();bindings.SelectHand(useLeft);
                            if(remapped)bindings.Assign(true,"order","touch:grip",true,out _);
                            typeof(CommandPanel).GetProperty("Bindings").SetValue(panel,bindings);quest.Bindings=bindings;
                            left.currentState=left.previousState=default;right.currentState=right.previousState=default;
                            var controller=useLeft?(OVRInput.OVRControllerBase)left:right;
                            var hand=useLeft?OVRInput.Controller.LTouch:OVRInput.Controller.RTouch;
                            var action=remapped?(useLeft?OVRInput.RawButton.LHandTrigger:OVRInput.RawButton.RHandTrigger):(useLeft?OVRInput.RawButton.X:OVRInput.RawButton.A);
                            tracking.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward)*Quaternion.Inverse(OVRInput.GetLocalControllerRotation(hand));
                            tracking.transform.position=board.Content.TransformPoint(new Vector3(point.x,0,point.y))+Vector3.up
                                -tracking.transform.rotation*OVRInput.GetLocalControllerPosition(hand);
                            session.CancelMode();session.Select(worker.id,false);session.Send(CommandKind.Stop);
                            Check(!session.CommandPulseActive,"Stop clears feedback for the superseded movement or attack");
                            Call(input,"ClearPointer");Frame();Frame();Frame();Frame();
                            Frame(action);
                            Check(worker.order==CommandKind.Move&&Pulse(point,false),"Touch Update: first order press moves immediately ("+useLeft+", remapped "+remapped+")");
                            int first=worker.activeOrderSequence;Frame(action);
                            Check(worker.activeOrderSequence==first,"Holding the action button does not repeat the order");
                            Frame();Frame(action);
                            Check(worker.order==CommandKind.AttackMove&&!session.HasTargetingMode&&worker.pendingOrders.Count==0&&Pulse(point,true)&&Normal(),
                                "Touch Update: second press issues Attack Move at the cursor without ever pressing Trigger");
                            int attack=worker.activeOrderSequence;Frame();Frame();
                            Check(worker.activeOrderSequence==attack&&session.Selection.Contains(worker.id),"Releasing double A/X preserves the attack and selection");
                            var before=worker.position;for(int tick=0;tick<4;tick++)session.Match.Step();
                            Check(worker.position.Distance(before)>.001f&&worker.order==CommandKind.AttackMove,"The issued attack advances simulation without another confirmation");

                            foreach(var destination in new[]{new Point(point.x+5,point.y),new Point(point.x+5,point.y+5),point})
                            {
                                Aim(destination);Frame(action);Frame();Frame(action);Frame();
                                Check(worker.order==CommandKind.AttackMove&&worker.destination.Distance(destination)<.01f&&Pulse(destination,true),
                                    "Rapid repeated A/X pairs attack each new destination without resetting controls ("+useLeft+", remapped "+remapped+")");
                            }
                            float originalZoom=board.Zoom;board.Zoom=.5f;Refresh();Call(input,"ClearPointer");
                            var drifted=new Point(point.x+2,point.y);
                            Aim(point);Frame();Frame(action);Frame();Aim(drifted);Frame(action);Frame();
                            Check(worker.order==CommandKind.AttackMove&&worker.destination.Distance(drifted)<.01f&&Pulse(drifted,true),
                                "Touch Update accepts a fast pair with physical aim drift on the small board ("+useLeft+", remapped "+remapped+")");
                            board.Zoom=originalZoom;Refresh();

                            void Aim(Point destination)
                            {
                                tracking.transform.position=board.Content.TransformPoint(new Vector3(destination.x,board.HeightAt(destination),destination.y))+Vector3.up
                                    -tracking.transform.rotation*OVRInput.GetLocalControllerPosition(hand);
                            }

                            void Frame(OVRInput.RawButton buttons=OVRInput.RawButton.None)
                            {
                                controller.previousState=controller.currentState;controller.currentState=default;controller.currentState.Buttons=(uint)buttons;
                                Call(input,"Update");Refresh();
                            }
                        }
                    }
                    finally
                    {
                        input.Wheel=null;Object.DestroyImmediate(wheel);Object.DestroyImmediate(tracking);
                        OVRInput.controllers=previousControllers;typeof(QuestBridge).GetProperty("TrackingSpace").SetValue(quest,previousTracking);
                        typeof(QuestBridge).GetProperty("Active").SetValue(quest,previousActive);console.transform.position=previousConsole;
                        typeof(CommandPanel).GetProperty("Bindings").SetValue(panel,previousBindings);quest.Bindings=previousQuestBindings;
                        Call(input,"ClearPointer");
                    }
                }
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
