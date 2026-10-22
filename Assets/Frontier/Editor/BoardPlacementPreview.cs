using TMPro;
using System;
using System.Collections.Generic;
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
    // Uses the real input router with isolated Touch states, without spatial-anchor I/O.
    public static class BoardPlacementPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);

        public static void RunBatch()
        {
            Run();
            StartupMenuPreview.Run();
            UnifiedWheelPreview.Render();
            Debug.Log("FRONTIER_BOARD_PLACEMENT_BATCH passed; no player build or physical Quest validation");
        }

        [MenuItem("Frontier/Check board placement controls")]
        public static void Run()
        {
            const string output="TestResults/BoardPlacement";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Board placement fixture");
            var boardObject=new GameObject("Placement board");var console=new GameObject("Placement console");
            var wheelObject=new GameObject("Placement callouts");var tracking=new GameObject("Synthetic placement tracking");
            var savedParent=new GameObject("Previous board anchor");
            var previousControllers=OVRInput.controllers;
            var previousBounds=Shader.GetGlobalVector("_FrontierBounds");var previousMatrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            var left=new OVRInput.OVRControllerLTouch();
            var right=(OVRInput.OVRControllerBase)Activator.CreateInstance(typeof(OVRInput).GetNestedType("OVRControllerRTouch",BindingFlags.NonPublic),true);
            int checks=0,captures=0;CommanderInput input=null;
            try
            {
                OVRInput.controllers=new List<OVRInput.OVRControllerBase>{left,right};
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;board.Viewer=preview.camera;Call(board,"Start");
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                typeof(QuestBridge).GetProperty("TrackingSpace").SetValue(quest,tracking.transform);
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Call(panel,"Start");
                var wheel=wheelObject.AddComponent<ActionWheel>();wheel.Session=session;wheel.Panel=panel;wheel.Quest=quest;
                input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Quest=quest;input.Panel=panel;input.Wheel=wheel;Call(input,"Start");
                var visual=Field<ControllerVisuals>(input,"controllerVisuals");
                visual.Root.SetParent(null,true);preview.AddSingleGO(visual.Root.gameObject);preview.AddSingleGO(wheelObject);
                var laser=Field<LineRenderer>(input,"laser");preview.AddSingleGO(laser.gameObject);

                foreach(bool useLeft in new[]{false,true})
                {
                    string hand=useLeft?"left":"right";
                    var active=useLeft?(OVRInput.OVRControllerBase)left:right;var inactive=useLeft?right:left;
                    var confirm=useLeft?OVRInput.RawButton.X:OVRInput.RawButton.A;
                    var cancel=useLeft?OVRInput.RawButton.Y:OVRInput.RawButton.B;
                    var trigger=useLeft?OVRInput.RawButton.LIndexTrigger:OVRInput.RawButton.RIndexTrigger;
                    var grip=useLeft?OVRInput.RawButton.LHandTrigger:OVRInput.RawButton.RHandTrigger;
                    var click=useLeft?OVRInput.RawButton.LThumbstick:OVRInput.RawButton.RThumbstick;
                    var bindings=new HotkeyBindings();bindings.SelectHand(useLeft);
                    bindings.Assign(true,"select","touch:b",true,out _);
                    bindings.Assign(true,"wheel","touch:grip",true,out _);
                    typeof(CommandPanel).GetProperty("Bindings").SetValue(panel,bindings);quest.Bindings=bindings;
                    active.currentState=active.previousState=inactive.currentState=inactive.previousState=default;
                    var rotation=Quaternion.Euler(55,0,0);
                    tracking.transform.rotation=rotation*Quaternion.Inverse(OVRInput.GetLocalControllerRotation(quest.ActiveController));
                    tracking.transform.position=new Vector3(0,1.35f,-.65f)-tracking.transform.rotation*OVRInput.GetLocalControllerPosition(quest.ActiveController);
                    Frame();Frame();
                    if(wheel.HintsEnabled)wheel.ToggleHints();
                    board.transform.SetParent(savedParent.transform,true);
                    board.transform.SetPositionAndRotation(new Vector3(.2f,1.055f,.1f),Quaternion.Euler(0,23,0));board.Width=1.25f;board.Placed=true;
                    quest.SetManualHeight(.2f);Set(quest,"useScene",true);
                    var originalPosition=board.transform.position;var originalRotation=board.transform.rotation;
                    active.previousState=active.currentState;active.currentState.Buttons=(uint)confirm;
                    active.previousState=active.currentState;
                    quest.BeginPlacement();float height=quest.PlacementHeight;
                    Frame(confirm);Frame(confirm);
                    Check(quest.Placing&&Near(quest.PlacementHeight,height)&&!session.Running,hand+": opening with a held A/X never confirms or starts the board");
                    Frame();Frame();Frame();
                    Check(Near(quest.PlacementHeight,1.05f),hand+": placement begins at the saved board height rather than the old manual preset");
                    Check(wheel.HintVisible&&!wheel.HintsEnabled&&!wheel.CanOpen,hand+": placement exposes its hints even when gameplay hints were hidden");
                    Call(input,"LateUpdate");
                    Check(visual.Root.gameObject.activeSelf&&Vector3.Distance(visual.Root.position,quest.PointerRay().origin)<.0001f,hand+": the actual controller remains attached and visible during placement");
                    Check(Vector3.Distance(laser.GetPosition(0),visual.EmitterPosition)<.0001f,hand+": placement laser begins at the controller emitter");
                    var labels=wheel.GetComponentsInChildren<Text>().Select(t=>t.text).ToArray();
                    Check(labels.Any(t=>t.StartsWith((useLeft?"X":"A")+" · ")&&t.ToLowerInvariant().Contains("place"))
                        &&labels.Any(t=>t.StartsWith((useLeft?"Y":"B")+" · ")&&t.ToLowerInvariant().Contains("cancel")),hand+": confirmation callouts use the physical face-button names despite remaps");
                    Check(labels.Any(t=>t.StartsWith("Grip")&&t.ToLowerInvariant().Contains("shrink"))
                        &&labels.Any(t=>t.StartsWith("Trigger")&&t.ToLowerInvariant().Contains("enlarge"))&&labels.Contains("↑↓ Height")
                        &&!labels.Any(t=>t.Contains("Attack Move")||t.Contains("Groups")||t.Contains("Selection")),hand+": callouts describe placement instead of gameplay commands");
                    height=quest.PlacementHeight;float width=board.Width;Frame(trigger,delta:.04f);
                    Check(quest.Placing&&Near(board.Width,width+.02f)&&Near(quest.PlacementHeight,height)&&!session.Running,hand+": Trigger enlarges without confirming or changing height");
                    Frame(trigger,delta:.04f);
                    Check(Near(board.Width,width+.04f),hand+": holding Trigger continues enlarging smoothly");
                    Frame();Frame(grip,delta:.04f);
                    Check(quest.Placing&&Near(board.Width,width+.02f)&&Near(quest.PlacementHeight,height),hand+": Grip shrinks without cancelling");
                    Frame();Frame(trigger|grip,delta:.04f);
                    Check(Near(board.Width,width+.02f)&&quest.Placing,hand+": opposite size buttons together leave size unchanged");
                    width=board.Width;Frame();Frame(stick:Vector2.up,delta:.04f);
                    Check(Near(quest.PlacementHeight,height+.012f)&&Near(board.Width,width),hand+": stick up raises height without resizing");
                    Frame(stick:Vector2.up,delta:.04f);
                    Check(Near(quest.PlacementHeight,height+.024f),hand+": holding the stick up keeps raising smoothly");
                    Frame(stick:Vector2.down,delta:.04f);
                    Check(Near(quest.PlacementHeight,height+.012f),hand+": stick down lowers height without cancelling");
                    height=quest.PlacementHeight;var rotationBefore=board.transform.rotation;
                    Frame(stick:new Vector2(.1f,.1f),delta:.04f);
                    Check(Near(quest.PlacementHeight,height)&&Quaternion.Angle(board.transform.rotation,rotationBefore)<.001f,hand+": a resting stick does not drift the board");
                    Frame(stick:Vector2.right,delta:.04f);
                    Check(Math.Abs(Quaternion.Angle(board.transform.rotation,rotationBefore)-1.8f)<.01f&&Near(quest.PlacementHeight,height),hand+": stick right rotates without changing height");
                    Frame(stick:Vector2.left,delta:.04f);
                    Check(Quaternion.Angle(board.transform.rotation,rotationBefore)<.001f,hand+": stick left reverses the rotation");
                    Frame(grip,Vector2.up,.04f);
                    Check(Near(board.Width,width-.02f)&&Near(quest.PlacementHeight,height+.012f),hand+": size and height can be adjusted together");
                    height=quest.PlacementHeight;width=board.Width;
                    board.Width=.605f;Frame(grip,delta:.04f);
                    Check(Near(board.Width,.6f),hand+": shrinking stops at the minimum board size");
                    board.Width=1.995f;Frame(trigger,delta:.04f);
                    Check(Near(board.Width,2),hand+": enlarging stops at the maximum board size");board.Width=width;
                    Frame();
                    inactive.currentState.Buttons=(uint)(useLeft?OVRInput.RawButton.A|OVRInput.RawButton.B|OVRInput.RawButton.RHandTrigger|OVRInput.RawButton.RIndexTrigger
                        :OVRInput.RawButton.X|OVRInput.RawButton.Y|OVRInput.RawButton.LHandTrigger|OVRInput.RawButton.LIndexTrigger);
                    if(useLeft)inactive.currentState.RThumbstick=new OVRPlugin.Vector2f{x=0,y=1};else inactive.currentState.LThumbstick=new OVRPlugin.Vector2f{x=0,y=1};
                    Frame();Check(quest.Placing&&Near(quest.PlacementHeight,height)&&Near(board.Width,width),hand+": the inactive hand cannot move, resize, confirm or cancel placement");inactive.currentState=inactive.previousState=default;
                    Frame(click);Frame(click);
                    Check(!wheel.HintVisible&&!wheel.HintsEnabled&&quest.Placing,hand+": physical stick click hides placement hints once without changing gameplay preference");
                    Frame();Frame(click);Frame();
                    Check(wheel.HintVisible&&!wheel.HintsEnabled,hand+": the next stick click restores placement hints");
                    Frame(stick:Vector2.up);Frame(stick:Vector2.right);Frame();
                    Check(!wheel.IsOpen&&quest.Placing,hand+": height and rotation stick input never opens the gameplay wheel");

                    // Emulate a room-surface hit: a manual adjustment must begin at
                    // that preview height and stop snapping back to the surface.
                    board.transform.position=new Vector3(.1f,.905f,0);Set(quest,"useScene",true);quest.AdjustPlacementHeight(.025f);
                    Check(Near(quest.PlacementHeight,.925f)&&!Field<bool>(quest,"useScene"),hand+": height adjustment starts from the current surface and takes manual control");
                    quest.AdjustPlacementHeight(100);Check(Near(quest.PlacementHeight,1.5f),hand+": raising clamps at 1.5 m");
                    quest.AdjustPlacementHeight(-100);Check(Near(quest.PlacementHeight,0),hand+": lowering clamps at floor height");
                    quest.AdjustPlacementHeight(1.05f);Frame();
                    Capture(hand+"-placement");

                    session.Pause("tracking",true);height=quest.PlacementHeight;width=board.Width;Frame(confirm|trigger,Vector2.up);Call(input,"LateUpdate");
                    Check(quest.Placing&&Near(quest.PlacementHeight,height)&&Near(board.Width,width)&&!wheel.HintVisible&&!visual.Root.gameObject.activeSelf,
                        hand+": tracking interruption blocks height/size/confirmation and hides controller hints");
                    session.Pause("tracking",false);Frame(confirm|trigger,Vector2.up);
                    Check(quest.Placing&&Near(quest.PlacementHeight,height)&&Near(board.Width,width),hand+": held buttons and stick remain blocked after tracking resumes");Frame();Frame();Frame();
                    Frame(cancel);
                    Check(!quest.Placing&&board.Placed&&board.transform.parent==savedParent.transform&&board.transform.position==originalPosition
                        &&Quaternion.Angle(board.transform.rotation,originalRotation)<.001f&&Near(board.Width,1.25f),hand+": physical B/Y cancels and restores the exact previous board transform");
                    Check(Near(Field<float>(quest,"manualHeight"),.2f)&&Field<bool>(quest,"useScene")&&!wheel.HintsEnabled,
                        hand+": cancelling also restores the prior height/surface settings and gameplay hint preference");
                    Frame();Frame();Frame();

                    session.StartMatch();var worker=session.Match.Entities.Values.First(e=>e.owner==0&&session.Match.Type(e).role=="worker");
                    session.Select(worker.id,false);panel.ToggleMenu();quest.BeginPlacement();Frame();Frame();Frame();
                    int tick=session.Match.Tick;var selection=session.Selection.ToArray();
                    height=quest.PlacementHeight;Frame(stick:Vector2.up,delta:.04f);Frame();session.Match.Step();
                    Check(quest.Placing&&Near(quest.PlacementHeight,height+.012f)&&session.Match.Tick==tick&&session.IsPaused,
                        hand+": height remains adjustable from pause without advancing the match");
                    // Keep native Quest persistence outside this Editor input test.
                    Set(quest,"busy",true);Frame(confirm);Set(quest,"busy",false);
                    Check(!quest.Placing&&board.Placed&&session.IsPaused&&session.Selection.SetEquals(selection),hand+": physical A/X confirms without releasing the menu pause or changing selection");
                    panel.ToggleMenu();Frame(confirm|trigger);Frame(confirm|trigger);
                    Check(session.Selection.SetEquals(selection)&&session.Match.Tick==tick&&!wheel.IsOpen,hand+": confirming face button and held trigger cannot leak into resumed gameplay");
                    Frame();Frame();Frame();
                    session.LeaveMatch();Frame();Frame();

                    void Frame(OVRInput.RawButton buttons=OVRInput.RawButton.None,Vector2 stick=default,float? delta=null)
                    {
                        active.previousState=active.currentState;active.currentState=default;active.currentState.Buttons=(uint)buttons;
                        if(useLeft)active.currentState.LThumbstick=new OVRPlugin.Vector2f{x=stick.x,y=stick.y};
                        else active.currentState.RThumbstick=new OVRPlugin.Vector2f{x=stick.x,y=stick.y};
                        // Editor fixtures run inside one frame. Use an explicit step
                        // only for continuous movement; entry/exit still use Update.
                        if(delta.HasValue)Call(input,"RoutePlacement",quest.PointerRay(),stick,Time.unscaledTime,delta.Value);
                        else Call(input,"Update");
                    }
                    void Capture(string name)
                    {
                        Call(input,"LateUpdate");var hint=Field<RectTransform>(wheel,"hintRoot");
                        preview.camera.orthographic=false;preview.camera.fieldOfView=48;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10;
                        preview.camera.transform.position=visual.Root.TransformPoint(new Vector3(useLeft?-.07f:.07f,.28f,-.28f));
                        preview.camera.transform.LookAt(visual.Root.TransformPoint(new Vector3(0,.01f,.01f)),visual.Root.up);
                        preview.camera.backgroundColor=new Color(.018f,.03f,.045f);preview.camera.clearFlags=CameraClearFlags.SolidColor;
                        HarvestingPreview.SaveCanvas(wheel.transform,hint,output+"/"+name+".png",preview,1700,950,preservePose:true);captures++;
                    }
                }
                Debug.Log($"FRONTIER_BOARD_PLACEMENT passed: {checks} checks, {captures} views; synthetic Touch input, no spatial-anchor writes or physical Quest validation");
                bool Near(float a,float b)=>Math.Abs(a-b)<.0002f;
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("PLACEMENT_PASS: "+message);}
            }
            finally
            {
                // Release controller visual/laser while their test OVR devices still exist.
                if(input!=null)Object.DestroyImmediate(input);
                Object.DestroyImmediate(wheelObject);Object.DestroyImmediate(console);Object.DestroyImmediate(boardObject);
                Object.DestroyImmediate(savedParent);Object.DestroyImmediate(fixture);Object.DestroyImmediate(tracking);preview.Cleanup();
                OVRInput.controllers=previousControllers;
                Shader.SetGlobalVector("_FrontierBounds",previousBounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",previousMatrix);
            }
        }
    }
}
