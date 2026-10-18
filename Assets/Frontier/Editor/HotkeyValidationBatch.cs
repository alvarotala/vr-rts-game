using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class HotkeyValidationBatch
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);

        [MenuItem("Frontier/Check editable hotkeys")]
        public static void Run()
        {
            bool had=PlayerPrefs.HasKey(HotkeyBindings.PreferenceKey);string saved=PlayerPrefs.GetString(HotkeyBindings.PreferenceKey,"");
            try
            {
                new HotkeyBindings().Save();CheckBindings();new HotkeyBindings().Save();
                MenuFlowPreview.Render();QueuedInputChecks.Run();ControlGroupPreview.Render();
            }
            finally
            {if(had)PlayerPrefs.SetString(HotkeyBindings.PreferenceKey,saved);else PlayerPrefs.DeleteKey(HotkeyBindings.PreferenceKey);PlayerPrefs.Save();}
        }
        private static void CheckBindings()
        {
            var fixture=new GameObject("Hotkey checks");var console=new GameObject("Hotkey console");
            var preview=new PreviewRenderUtility();Keyboard keyboard=null;Mouse mouse=null;int checks=0;
            // Use player update counters for synthetic input while this fixture runs in
            // edit mode. Restore the package flag, devices and preferences in finally.
            var manager=typeof(InputSystem).GetField("s_Manager",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            var playerUpdates=manager.GetType().GetProperty("runPlayerUpdatesInEditMode",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            bool previousUpdates=(bool)playerUpdates.GetValue(manager);
            try
            {
                playerUpdates.SetValue(manager,true);
                new HotkeyBindings().Save();Directory.CreateDirectory("TestResults/Hotkeys");
                var profile=new HotkeyBindings();
                foreach(bool touch in new[]{false,true})
                    Check(HotkeyBindings.Definitions(touch).Select(d=>profile.Get(touch,d.Id)).Distinct().Count()==HotkeyBindings.Definitions(touch).Count,"Unique default bindings for "+touch);
                Check(!profile.Assign(true,"select","touch:a",false,out _)&&profile.Name(true,"select")=="Trigger","Conflicts do not silently overwrite a binding");
                Check(profile.Assign(true,"select","touch:a",true,out _)&&profile.Name(true,"order")=="Trigger","Touch swap preserves both actions");
                Check(!profile.Assign(false,"add","key:Q",true,out _)&&!profile.Assign(false,"order","key:LeftShift",true,out _),"Modifier constraints prevent ambiguous combinations");
                Check(profile.Assign(false,"add","key:LeftAlt",false,out _),"Modifiers can be rebound");
                Check(profile.Assign(false,"order","key:Q",false,out _),"Keyboard and mouse share gameplay binding slots");
                var loaded=HotkeyBindings.FromJson(profile.ToJson());
                Check(loaded.Get(false,"order")=="key:Q"&&loaded.Get(true,"select")=="touch:a","Both device profiles survive serialization");
                loaded.Reset(true);Check(loaded.Get(true,"select")=="touch:trigger"&&loaded.Get(false,"order")=="key:Q","Reset affects only the selected device");
                Check(HotkeyBindings.FromJson("invalid json").Get(false,"order")=="mouse:right","Corrupt JSON falls back to defaults");
                Check(HotkeyBindings.FromJson(profile.ToJson().Replace("key:Q","key:Digit1")).Get(false,"order")=="mouse:right","Duplicate saved inputs cannot install a broken profile");

                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=fixture.AddComponent<BoardView>();board.Session=session;Call(board,"Start");
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);preview.camera.transform.position=new Vector3(0,1.7f,-2);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Call(panel,"Start");preview.AddSingleGO(console);
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Quest=quest;input.Panel=panel;
                keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
                Refresh();Click("Hotkeys");
                Check(!session.Running&&panel.Page==CommandPanel.MenuPage.Hotkeys,"Hotkeys opens before a match");Capture("01-desktop-bindings");
                Click("Change: order");Neutral();panel.CaptureHotkeyInput(Miss());
                Keys(Key.Q);panel.CaptureHotkeyInput(Miss());
                Check(panel.Bindings.Get(false,"order")=="key:Q"&&HotkeyBindings.Load().Get(false,"order")=="key:Q","Captured keyboard input applies and persists immediately: "+panel.Bindings.Get(false,"order")+" / "+Field<string>(panel,"hotkeyMessage")+" / ready="+Field<bool>(panel,"hotkeyCaptureReady")+" / key="+HotkeyInput.Pressed(false)+" / held="+keyboard.qKey.isPressed);
                Check(!session.Running&&!session.IsPaused,"Capturing a pre-match key never starts a match");
                Neutral();panel.CaptureHotkeyInput(Miss());Click("Change: order");Neutral();panel.CaptureHotkeyInput(Miss());
                Keys(Key.W);panel.CaptureHotkeyInput(Miss());
                Check(panel.Bindings.Get(false,"order")=="key:Q"&&Field<string>(panel,"pendingHotkey")=="key:W","A conflicting press waits for explicit confirmation");Capture("02-swap-confirmation");
                Click("Cancel change");Check(panel.Bindings.Get(false,"pan-up")=="key:W","Cancelling a swap preserves both bindings");Drain();
                Click("Change: order");Neutral();panel.CaptureHotkeyInput(Miss());Keys(Key.Escape);panel.CaptureHotkeyInput(Miss());
                Check(panel.Page==CommandPanel.MenuPage.Hotkeys&&panel.Bindings.Get(false,"order")=="key:Q","Escape cancels capture without navigating or changing the input");Drain();
                Click("Change: order");Set(panel,"hotkeyDeadline",Time.unscaledTime-1);panel.CaptureHotkeyInput(Miss());
                Check(Field<string>(panel,"editingHotkey")==null,"Capture times out safely");Drain();
                Click("Change: order");Call(panel,"OnApplicationFocus",false);
                Check(Field<string>(panel,"editingHotkey")==null&&panel.Bindings.Get(false,"order")=="key:Q","Focus loss cancels an unfinished capture");Drain();
                Click("Change: select");Neutral();panel.CaptureHotkeyInput(Miss());
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Middle));Keys();panel.CaptureHotkeyInput(Miss());
                Check(panel.Bindings.Get(false,"select")=="mouse:middle","Mouse button capture is supported");Drain();
                Click("Change: add");Neutral();panel.CaptureHotkeyInput(Miss());Keys(Key.W);panel.CaptureHotkeyInput(Miss());
                Check(Field<string>(panel,"pendingHotkey")==null&&panel.Bindings.Get(false,"add")=="key:LeftShift","An invalid modifier never offers an impossible swap");
                Click("Cancel change");Drain();
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);Call(panel,"Rebuild");Capture("03-touch-bindings");
                Click("Change: select");Call(panel,"OfferHotkey","touch:a");Click("Confirm change");
                Check(panel.Bindings.Name(true,"select")=="A"&&panel.Bindings.Name(true,"order")=="Trigger","Confirmed Touch swap updates both inputs");Drain();
                Click("Hotkeys: Selection");
                Check(Field<Text[]>(panel,"hotkeyBindings")[0].text.StartsWith("A on map"),"Reference follows the customized Touch selection input");
                Click("Restore device defaults");Capture("04-restore-defaults");Click("Confirm change");Drain();
                Check(panel.Bindings.Name(true,"select")=="Trigger"&&panel.Bindings.Get(false,"order")=="key:Q","UI reset preserves the other device profile");
                Click("Back");Click("Settings");Click("Hand: Right");
                Check(quest.ActiveController==OVRInput.Controller.LTouch&&HotkeyBindings.Load().LeftHand,"Selected hand persists and controls the pointer's controller source");
                Capture("06-left-settings");Click("Back");Click("Hotkeys");
                Check(Field<Text>(panel,"hotkeysDevice").text.Contains("Left Touch")&&
                    Field<Text[]>(panel,"hotkeyBindings")[1].text.StartsWith("X ")&&Field<Text[]>(panel,"hotkeyBindings")[3].text.StartsWith("Y "),"Hotkeys shows only the active left hand with X/Y labels");
                Check(!panel.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="Quest Touch"||t.name=="Mouse / Keyboard"),"Inactive device tabs are absent");
                Capture("07-left-hotkeys");
                CheckSingleController();
                Click("Restore device defaults");Click("Confirm change");Drain();
                Check(panel.Bindings.LeftHand&&panel.Bindings.Name(true,"order")=="X","Restoring bindings keeps the chosen hand");
                Click("Hotkeys: Board");
                Check(Field<Text[]>(panel,"hotkeyBindings")[1].text.StartsWith("Left stick"),"Reference instructions follow the active stick");
                Click("Back");Click("Settings");Click("Hand: Left");Click("Back");Click("Hotkeys");
                Check(panel.Bindings.Name(true,"order")=="A"&&quest.ActiveController==OVRInput.Controller.RTouch,"Switching back restores right-side button names without changing bindings");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);
                Click("Back");Click("New Match");Click("Hotkeys");Click("Back");
                Check(panel.Page==CommandPanel.MenuPage.MatchSetup,"Pre-match setup returns to the same setup");
                board.Placed=true;Click("START MATCH");var match=session.Match;
                var worker=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");session.Select(worker.id,false);session.Group(0,true);
                Click("Pause");Click("Settings");int handTick=match.Tick;Click("Hand: Right");match.Step();
                Check(session.IsPaused&&match.Tick==handTick&&panel.Bindings.LeftHand,"Changing hands from pause keeps the same match paused");
                Click("Back");Click("Hotkeys");int tick=match.Tick;int minerals=session.View.minerals;
                Click("Change: select");Neutral();panel.CaptureHotkeyInput(Miss());Keys(Key.E);panel.CaptureHotkeyInput(Miss());Drain();
                for(int i=0;i<30;i++)match.Step();
                Check(session.IsPaused&&match.Tick==tick&&session.View.minerals==minerals&&session.Selection.Contains(worker.id)&&session.GroupSize(0)==1,"Editing during pause preserves simulation, resources, selection and groups");
                Capture("05-paused-bindings");Click("Back");
                Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"Back returns to pause without resuming");
                Check(Field<Text[]>(panel,"controlGuideGestures")[0].text.StartsWith("E on map"),"Pause guide displays current bindings");
                Click("Resume match");
                Check(session.Match==match&&!session.IsPaused&&panel.AcceptsGameInput,"Resume continues the original match");

                // Exercise the real Update router with synthetic Input System state.
                panel.Bindings.Assign(false,"group1","key:Z",false,out _);panel.Bindings.Assign(false,"pause","key:P",false,out _);
                Field<RectTransform>(panel,"root").position=Vector3.one*100;
                preview.camera.transform.SetPositionAndRotation(new Vector3(0,3,-2),Quaternion.Euler(70,0,0));
                session.Select(0,false);Frame();Frame();Frame(Key.Digit1);
                Check(session.Selection.Count==0,"Old group hotkey no longer fires");Frame();Frame(Key.Z);
                Check(session.Selection.Contains(worker.id),"Rebound group hotkey reaches the real input router");Frame();Frame(Key.Space);
                Check(panel.AcceptsGameInput,"Old pause hotkey no longer fires");Frame();Frame(Key.P);
                Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"Rebound pause hotkey pauses the match");
                Frame();Frame(Key.P);Check(panel.AcceptsGameInput,"Rebound pause hotkey also resumes from pause");Frame();Frame(Key.P);
                Frame();Frame(Key.Escape);Check(panel.AcceptsGameInput,"Fixed Escape resumes from pause after rebinding");
                Frame();Keys(Key.RightAlt);Check(HotkeyInput.Read("key:LeftAlt").Held,"Right-side modifiers use the same configured binding");Neutral();
                Check(HotkeyInput.Read(panel.Bindings,false,"order").Held==false,"Unpressed rebound order stays idle");Keys(Key.Q);
                Check(HotkeyInput.Read(panel.Bindings,false,"order").Down,"Order reader uses the saved mapping");Neutral();

                var entity=match.Entities[worker.id];entity.position=new Point(12.5f,14.5f);
                typeof(GameSession).GetProperty("View").SetValue(session,match.View(0));Array.Fill(session.View.visibility,(byte)2);Call(board,"LateUpdate");
                preview.camera.transform.SetPositionAndRotation(board.transform.position+Vector3.up*3,Quaternion.Euler(90,0,0));preview.camera.orthographic=true;preview.camera.orthographicSize=2;
                session.Select(0,false);Frame();Frame();Aim(entity.position);Frame(Key.E);Frame();
                Check(session.Selection.Contains(worker.id),"Rebound selection key selects the pointed unit through Update");
                session.Send(CommandKind.Stop);Aim(new Point(20.5f,16.5f));Frame();Frame(Key.Q);
                Check(entity.order==CommandKind.Move,"Rebound order key issues a real contextual movement command");
                Frame();session.Send(CommandKind.Stop);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=mouse.position.ReadValue()}.WithButton(MouseButton.Right));Frame();
                Check(entity.order==CommandKind.Stop,"Old order button no longer commands the map");Neutral();

                Debug.Log($"FRONTIER_HOTKEYS passed: {checks} checks; synthetic desktop input and rendered UI, no physical Quest input");
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("HOTKEY_PASS: "+message);}
                void Refresh(){Set(panel,"nextText",0f);Call(panel,"LateUpdate");}
                void Click(string name)
                {
                    Refresh();var rect=panel.GetComponentsInChildren<RectTransform>().First(t=>t.name==name&&t.gameObject.activeInHierarchy);
                    var center=rect.TransformPoint(rect.rect.center);var ray=new Ray(center-rect.forward,rect.forward);
                    if(!panel.Point(ray,true,false)||!panel.Point(ray,false,true))throw new Exception("Missed button: "+name);Refresh();
                }
                void Capture(string name){Refresh();HarvestingPreview.SavePanel(panel,"TestResults/Hotkeys/"+name+".png",preview);}
                Ray Miss()=>new Ray(Vector3.one*100,Vector3.forward);
                void Keys(params Key[] keys)
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();
                    // Native editor events may make a physical device current during an
                    // update. Keep this fixture isolated from the user's live devices.
                    keyboard.MakeCurrent();mouse.MakeCurrent();
                }
                void Neutral(){InputSystem.QueueStateEvent(mouse,new MouseState());Keys();}
                void Drain(){Neutral();panel.CaptureHotkeyInput(Miss());}
                void Frame(params Key[] keys){Keys(keys);Call(input,"Update");}
                void Aim(Point point)
                {
                    Vector2 screen=preview.camera.WorldToScreenPoint(board.Content.TransformPoint(new Vector3(point.x,0,point.y)));
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});Frame();
                }
                void CheckSingleController()
                {
                    // Exercise Meta's actual virtual-to-physical maps with isolated
                    // controller states. No headset or native tracking is simulated.
                    var previous=OVRInput.controllers;
                    var left=new OVRInput.OVRControllerLTouch();
                    var right=(OVRInput.OVRControllerBase)Activator.CreateInstance(typeof(OVRInput).GetNestedType("OVRControllerRTouch",BindingFlags.NonPublic),true);
                    OVRInput.controllers=new System.Collections.Generic.List<OVRInput.OVRControllerBase>{left,right};
                    try
                    {
                        right.currentState.Buttons=(uint)(OVRInput.RawButton.A|OVRInput.RawButton.B|OVRInput.RawButton.RIndexTrigger|OVRInput.RawButton.RHandTrigger|OVRInput.RawButton.RThumbstick);
                        right.currentState.RThumbstick=new OVRPlugin.Vector2f{x=0,y=1};
                        Check(!HotkeyInput.AnyHeld(true,quest.ActiveController)&&HotkeyInput.Pressed(true,quest.ActiveController)==null,"Inactive right buttons never enter capture or held-state checks");
                        Check(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,quest.ActiveController)==Vector2.zero,"Inactive stick does not drive placement or wheels");
                        Click("Change: order");Neutral();panel.CaptureHotkeyInput(Miss());panel.CaptureHotkeyInput(Miss());
                        Check(Field<string>(panel,"editingHotkey")=="order"&&Field<string>(panel,"pendingHotkey")==null,"Inactive buttons cannot rebind the active controller");
                        left.currentState.Buttons=(uint)OVRInput.RawButton.Y;panel.CaptureHotkeyInput(Miss());
                        Check(Field<string>(panel,"pendingHotkey")=="touch:b"&&Field<Text>(panel,"hotkeyModalDetail").text.StartsWith("Y is used"),"Active left Y is captured and conflicts use left-side labels");
                        Click("Cancel change");left.currentState=default;Drain();
                        Check(!Field<bool>(panel,"hotkeyReleasePending"),"Holding the inactive controller cannot block release draining");
                        var ids=new[]{"select","order","add","back","wheel"};
                        var buttons=new[]{OVRInput.RawButton.LIndexTrigger,OVRInput.RawButton.X,OVRInput.RawButton.LHandTrigger,OVRInput.RawButton.Y,OVRInput.RawButton.LThumbstick};
                        for(int i=0;i<ids.Length;i++)
                        {
                            left.previousState=default;left.currentState=default;left.currentState.Buttons=(uint)buttons[i];
                            var signal=HotkeyInput.Read(panel.Bindings,true,ids[i]);
                            Check(signal.Down&&signal.Held,"Active left button reaches logical action "+ids[i]);
                            left.previousState=left.currentState;left.currentState=default;
                            Check(HotkeyInput.Read(panel.Bindings,true,ids[i]).Up,"Active left release reaches logical action "+ids[i]);
                        }
                        left.previousState=default;left.currentState.LThumbstick=new OVRPlugin.Vector2f{x=1,y=0};
                        Check(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,quest.ActiveController).x>.9f,"Active left stick drives the same directional gestures");
                        panel.Bindings.SelectHand(false);right.currentState=default;
                        left.currentState.Buttons=(uint)OVRInput.RawButton.X;
                        Check(!HotkeyInput.Read(panel.Bindings,true,"order").Held&&!HotkeyInput.AnyHeld(true,quest.ActiveController),"After switching right, the left hand is ignored");
                        right.currentState.Buttons=(uint)OVRInput.RawButton.A;
                        Check(HotkeyInput.Read(panel.Bindings,true,"order").Down,"Active right A maps to the same logical order");
                    }
                    finally{OVRInput.controllers=previous;panel.Bindings.SelectHand(true);}
                }
            }
            finally
            {
                if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
                playerUpdates.SetValue(manager,previousUpdates);
                Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();
            }
        }
    }
}
