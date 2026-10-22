using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class StartupMenuPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
        public static void Run(){Render();SavedBoardChecks();MenuFlowPreview.Render();ComfortAudioPreview.Render();}

        [Serializable] private sealed class WindowPlacementProbe { public int version;public Vector3 offset; }

        [MenuItem("Frontier/Preview startup menu position")]
        public static void Render()
        {
            const string key="frontier.panel-placement.v1",output="TestResults/StartupMenu";
            bool had=PlayerPrefs.HasKey(key);string saved=PlayerPrefs.GetString(key,"");
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Startup menu fixture");
            GameObject console=null;Camera createdCamera=null;int checks=0,captures=0;
            try
            {
                Directory.CreateDirectory(output);
                const string legacyProfile="{\"offset\":{\"x\":0.45,\"y\":0.9,\"z\":-0.6}}";
                PlayerPrefs.SetString(key,legacyProfile);
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var anchor=new GameObject("Restored spatial anchor").transform;anchor.SetParent(fixture.transform);
                anchor.SetPositionAndRotation(new Vector3(-12,.75f,19),Quaternion.Euler(0,43,0));
                var boardObject=new GameObject("Board");boardObject.transform.SetParent(anchor,false);
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Call(board,"Start");board.Width=1.4f;board.Fit();board.Placed=true;
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                if(quest.Camera==null)Call(quest,"Awake");createdCamera=quest.Camera;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                var eye=preview.camera.transform;eye.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var panel=CreatePanel();var root=Field<RectTransform>(panel,"root");
                var boardPose=board.transform.localToWorldMatrix;
                Call(panel,"UpdatePosition",false);
                Check(!root.gameObject.activeSelf&&!panel.Point(new Ray(Vector3.zero,Vector3.forward),true,false),
                    "Startup waits for a tracked head pose and cannot click the hidden panel");
                eye.SetPositionAndRotation(new Vector3(8,1.65f,-6),Quaternion.Euler(8,132,0));Refresh();CheckInFront();
                var expectedCenter=Center();var expectedScale=root.localScale;var windowParent=root.parent;
                var pageHeights=new System.Collections.Generic.HashSet<float>();
                CheckShared("Initial main menu");
                Check(panel.Page==CommandPanel.MenuPage.Main&&!session.Running&&!quest.Placing&&!board.Content.gameObject.activeSelf,
                    "Startup offers the main menu without forcing board placement");
                Check(board.transform.localToWorldMatrix==boardPose&&PlayerPrefs.GetString(key)==legacyProfile,
                    "Legacy board-relative placement is ignored without rewriting it or moving the board");
                Capture("01-shared-window-start");

                Click("New Match");Check(panel.Page==CommandPanel.MenuPage.MatchSetup,"New Match opens setup");Click("Back");
                Click("Settings");Click("Hotkeys");Check(panel.Page==CommandPanel.MenuPage.Hotkeys,"Settings opens Hotkeys");Click("Back");
                Click("Play space");
                var previousBoard=board.transform.localToWorldMatrix;var previousParent=board.transform.parent;float previousWidth=board.Width;
                Click("Reposition board");Check(quest.Placing,"Reposition board is reachable before a match");
                quest.PlaceRay(new Ray(eye.position+Vector3.up,Vector3.down),0,Vector2.zero,false);Refresh();CheckShared("Aiming a board placement");
                quest.CancelPlacement();Refresh();CheckShared("Cancelling board placement");
                Check(board.transform.localToWorldMatrix==previousBoard&&board.transform.parent==previousParent&&board.Width==previousWidth,
                    "Cancelling placement restores the anchored board without changing the common window");
                Click("Back");Click("Guides");Click("Control guide");
                Check(panel.Page==CommandPanel.MenuPage.Guide,"Guides opens the taller control guide");Click("Back");Click("Back");

                anchor.SetPositionAndRotation(new Vector3(25,.9f,-17),Quaternion.Euler(0,-87,0));board.Width=.65f;board.Fit();Refresh();
                CheckShared("Late anchor restoration and board resizing");
                Check(PlayerPrefs.GetString(key)==legacyProfile,"Navigation and anchor restoration do not migrate the old preference implicitly");
                CheckInterruptedDrag(panel.CancelPointerInteraction,"Cancelling pointer input during a lobby drag");
                Drag(.3f);string lobbyProfile=PlayerPrefs.GetString(key);
                var lobbyOffset=JsonUtility.FromJson<WindowPlacementProbe>(lobbyProfile);
                Check(lobbyOffset.version==2&&Vector3.Distance(eye.position+HorizontalFacing()*lobbyOffset.offset,Center())<.0001f,
                    "Moving the lobby writes version 2 relative to the viewer's horizontal frame");
                session.StartMatch();Refresh();CheckShared("Starting the match after moving the lobby");
                Check(panel.Page==CommandPanel.MenuPage.Actions,"Starting a match opens the HUD in the same window");
                var hq=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                session.Select(hq.id,false);Refresh();CheckShared("Selecting a producer adds production without moving the center");
                Capture("02-shared-window-match");
                session.Select(0,false);Refresh();CheckShared("Clearing selection changes HUD height around the same center");
                Click("Pause");Click("Settings");Click("Hotkeys");Click("Back");Click("Back");
                Click("Guides");Click("Control guide");Click("Back");Click("Back");Click("Resume match");
                Check(panel.Page==CommandPanel.MenuPage.Actions&&!session.IsPaused,"Returning from nested pause pages resumes the match");

                CheckInterruptedDrag(panel.ToggleMenu,"Changing to Pause during a match drag");
                Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"A page transition during dragging still opens Pause");
                Click("Resume match");
                Drag(-.22f);string matchProfile=PlayerPrefs.GetString(key);
                Check(matchProfile!=lobbyProfile&&JsonUtility.FromJson<WindowPlacementProbe>(matchProfile).version==2,
                    "Moving the match window updates the same shared preference");
                anchor.SetPositionAndRotation(new Vector3(-32,.4f,28),Quaternion.Euler(0,110,0));board.Width=1.8f;board.Fit();Refresh();
                CheckShared("Moving and scaling the board during a match");
                CheckInterruptions("Match");
                Click("Pause");Click("Return to main menu");Click("Leave match");
                Check(panel.Page==CommandPanel.MenuPage.Main&&!session.Running,"Leaving a match keeps its moved window in the main menu");
                Check(PlayerPrefs.GetString(key)==matchProfile,"Returning to the lobby keeps the match's shared placement preference");
                Capture("03-shared-window-return");
                CheckInterruptions("Main menu");

                var nearbyPosition=Center();eye.position+=new Vector3(.3f,.1f,-.2f);eye.rotation=Quaternion.Euler(0,-17,0);Refresh();
                CheckShared("Nearby walking and looking around");eye.rotation=Quaternion.Euler(0,140,0);Refresh();CheckShared("Turning around in place");
                Check(Vector3.Distance(Center(),nearbyPosition)<.0001f,"The common window stays in the room instead of following the head");
                session.StartMatch();Refresh();CheckShared("Restarting from the retained main menu");
                session.Send(Frontier.Core.CommandKind.Surrender);Refresh();CheckShared("Showing the result page");
                Check(panel.Page==CommandPanel.MenuPage.Result,"A completed match enters the result page");Click("Main menu");
                Check(pageHeights.Count>=4,"The same center and scale survive at least four distinct content heights");

                session.StartMatch();Refresh();CheckShared("Starting before a relocalization");
                eye.SetPositionAndRotation(new Vector3(-18,1.7f,22),Quaternion.Euler(0,-40,0));Refresh();CheckInFront();expectedCenter=Center();
                CheckShared("Far head relocalization during a match");
                Check(PlayerPrefs.GetString(key)==matchProfile,"Automatic recovery does not overwrite the explicit dragged preference");
                session.LeaveMatch();Refresh();CheckShared("Leaving after relocalization");
                for(int step=0;step<20;step++){eye.position+=Vector3.right*.3f;Refresh();}
                Check(Vector3.Distance(Center(),eye.position)<3.25f,"Gradual localization drift cannot leave the common window out of reach");
                expectedCenter=Center();CheckShared("Recovered common window");
                panel.Recall();Refresh();CheckInFront();expectedCenter=Center();CheckShared("Explicit menu recall");
                Check(!PlayerPrefs.HasKey(key),"Recall clears the moved preference and restores the default viewer-relative position");
                Drag(.2f);string restoredProfile=PlayerPrefs.GetString(key);var restoredOffset=JsonUtility.FromJson<WindowPlacementProbe>(restoredProfile).offset;
                Object.DestroyImmediate(console);eye.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                panel=CreatePanel();root=Field<RectTransform>(panel,"root");Call(panel,"UpdatePosition",false);
                Check(!root.gameObject.activeSelf,"A saved version 2 offset still waits for the first tracked pose");
                eye.SetPositionAndRotation(new Vector3(-9,1.55f,-15),Quaternion.Euler(14,75,0));Refresh();
                expectedCenter=eye.position+HorizontalFacing()*restoredOffset;expectedScale=root.localScale;windowParent=root.parent;
                CheckShared("Restoring the dragged preference in a new room pose");
                Check(PlayerPrefs.GetString(key)==restoredProfile,"Restoring a version 2 offset does not rewrite it");
                Capture("04-restored-shared-window");
                board.Placed=false;Click("New Match");Click("START MATCH");
                Check(quest.StartPending&&quest.Placing&&!session.Running,"Without a saved board, Start Match still defers to board placement");
                quest.CancelPlacement();Refresh();CheckShared("Cancelling deferred match placement");Click("Back");

                Object.DestroyImmediate(console);PlayerPrefs.DeleteKey(key);
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);
                eye.SetPositionAndRotation(new Vector3(3,1.6f,-4),Quaternion.Euler(10,-32,0));
                panel=CreatePanel();root=Field<RectTransform>(panel,"root");Refresh();CheckInFront();
                expectedCenter=Center();expectedScale=root.localScale;windowParent=root.parent;
                CheckShared("Desktop startup uses the same viewer-relative window");
                Click("Settings");Click("Hotkeys");Click("Back");Click("Back");
                session.StartMatch();Refresh();CheckShared("Desktop match starts in the same window");
                Drag(.15f);Click("Pause");Click("Return to main menu");Click("Leave match");
                CheckShared("Desktop return retains the moved match window");
                Debug.Log($"FRONTIER_STARTUP_MENU passed: {checks} checks, {captures} views; synthetic head poses, no physical Quest input");

                CommandPanel CreatePanel()
                {
                    console=new GameObject("Startup console");preview.AddSingleGO(console);
                    var created=console.AddComponent<CommandPanel>();created.Session=session;created.Board=board;created.Quest=quest;created.PlaceRequested=quest.BeginPlacement;Call(created,"Start");return created;
                }
                void Refresh(){typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Call(panel,"LateUpdate");}
                void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;Debug.Log("STARTUP_MENU_PASS: "+message);}
                Vector3 Center()=>root.TransformPoint(root.rect.center);
                Quaternion HorizontalFacing()=>Quaternion.LookRotation(Vector3.ProjectOnPlane(eye.forward,Vector3.up).normalized,Vector3.up);
                void CheckInFront()
                {
                    var front=eye.position+HorizontalFacing()*new Vector3(0,-.12f,1.25f);
                    Check(root.gameObject.activeSelf&&Vector3.Distance(Center(),front)<.0001f&&Vector3.Dot(root.forward,(Center()-eye.position).normalized)>.999f,
                        "The window center appears 1.25 m in front of the current viewer and faces them");
                }
                void CheckShared(string context)
                {
                    Check(root.gameObject.activeSelf&&Vector3.Distance(Center(),expectedCenter)<.0001f&&root.localScale==expectedScale&&root.parent==windowParent,
                        context+": all pages retain the common world center, scale and parent");
                    Check(root.rect.center.sqrMagnitude<.0001f&&Vector3.Distance(root.position,Center())<.0001f,
                        context+": content grows around its center rather than a corner");
                    var handle=Field<RectTransform>(panel,"moveHandle");var reset=Field<RectTransform>(panel,"resetHandle");
                    Check(handle.gameObject.activeInHierarchy&&handle.rect.size==new Vector2(320,80)&&!reset.gameObject.activeSelf,
                        context+": every page uses the shared move handle without a reset button");
                    pageHeights.Add(root.rect.height);
                }
                void CheckInterruptions(string context)
                {
                    Call(panel,"OnApplicationFocus",false);Call(panel,"OnApplicationFocus",true);Refresh();CheckShared(context+" after focus recovery");
                    Call(panel,"OnApplicationPause",true);Call(panel,"OnApplicationPause",false);Refresh();CheckShared(context+" after app resume");
                    Call(panel,"UpdatePosition",false);
                    Check(!root.gameObject.activeSelf&&!panel.Point(new Ray(eye.position,eye.forward),true,false),context+": untracked UI is hidden and cannot receive clicks");
                    Refresh();CheckShared(context+" after nearby tracking recovery");
                    session.Pause("headset",true);Refresh();
                    Check(root.gameObject.activeSelf==(!quest.Active||session.Running)
                        &&Vector3.Distance(Center(),expectedCenter)<.0001f&&root.localScale==expectedScale,
                        context+": a tracked headset pause preserves the window pose and keeps the match's pause information visible");
                    session.Pause("headset",false);Refresh();CheckShared(context+" after replacing the headset");
                }
                void CheckInterruptedDrag(Action interrupt,string context)
                {
                    var before=Center();var boardBefore=board.transform.localToWorldMatrix;var ray=RayTo("Move menu");
                    Check(panel.Point(ray,true,false)&&panel.IsDragging,context+": starts the drag");
                    var displaced=new Ray(ray.origin+root.right*.16f,ray.direction);
                    Check(panel.Point(displaced,false,false)&&panel.IsDragging&&Vector3.Distance(Center(),before)>.12f,
                        context+": the unreleased pointer moves the window");
                    var stoppedAt=Center();interrupt();
                    Check(!panel.IsDragging&&Vector3.Distance(Center(),stoppedAt)<.0001f,
                        context+": interruption ends dragging at its latest center");
                    expectedCenter=stoppedAt;Refresh();CheckShared(context);
                    var persisted=JsonUtility.FromJson<WindowPlacementProbe>(PlayerPrefs.GetString(key));
                    Check(persisted!=null&&persisted.version==2
                        &&Vector3.Distance(eye.position+HorizontalFacing()*persisted.offset,stoppedAt)<.0001f,
                        context+": interruption saves the latest shared placement");
                    var neutral=new Ray(displaced.origin+root.right*.7f,displaced.direction);
                    panel.Point(neutral,false,false);Refresh();CheckShared(context+" after neutral pointer movement");
                    panel.Point(neutral,false,true);Refresh();CheckShared(context+" after the stale release");
                    Check(!panel.IsDragging&&board.transform.localToWorldMatrix==boardBefore,
                        context+": neither later pointer movement nor release restarts dragging or moves the board");
                }
                void Drag(float distance)
                {
                    var before=Center();var boardBefore=board.transform.localToWorldMatrix;var ray=RayTo("Move menu");
                    Check(panel.Point(ray,true,false)&&panel.IsDragging,"The common move handle starts dragging");
                    Check(panel.Point(new Ray(ray.origin+root.right*distance,ray.direction),false,true),"Releasing the common handle completes the drag");
                    Refresh();Check(!panel.IsDragging&&Vector3.Distance(Center(),before)>Mathf.Abs(distance)*.8f&&board.transform.localToWorldMatrix==boardBefore,
                        "Dragging moves only the common window and preserves the board");
                    expectedCenter=Center();CheckShared("Moved common window");
                }
                Ray RayTo(string name)
                {
                    var target=panel.GetComponentsInChildren<RectTransform>().Single(r=>r.name==name&&r.gameObject.activeInHierarchy);
                    var center=target.TransformPoint(target.rect.center);return new Ray(center-target.forward*.6f,target.forward);
                }
                void Click(string name){Refresh();var ray=RayTo(name);Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Can click "+name);Refresh();CheckShared("After "+name);}
                void Capture(string name)
                {
                    var position=root.position;var rotation=root.rotation;var scale=root.localScale;
                    preview.camera.orthographic=false;preview.camera.fieldOfView=60;preview.camera.nearClipPlane=.02f;preview.camera.farClipPlane=60;
                    HarvestingPreview.SaveCanvas(panel.transform,root,output+"/"+name+".png",preview,1400,900,preservePose:true);captures++;
                    Check(root.position==position&&root.rotation==rotation&&root.localScale==scale,"Capturing preserves the actual shared window pose");
                }
            }
            finally
            {
                if(console!=null)Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);
                if(createdCamera!=null)Object.DestroyImmediate(createdCamera.gameObject);preview.Cleanup();
                if(had)PlayerPrefs.SetString(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
            }
        }

        public static void SavedBoardChecks()
        {
            var fixture=new GameObject("Saved board relocation checks");
            var boardObject=new GameObject("Saved board");boardObject.transform.SetParent(fixture.transform);
            string path=Path.Combine(Path.GetTempPath(),"frontier-placement-check-"+Guid.NewGuid()+".json");
            Camera camera=null;int checks=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                if(quest.Camera==null)Call(quest,"Awake");camera=quest.Camera;
                const string saved="{\"schemaVersion\":1,\"anchorUuid\":\"28d768d0-93f3-4d1a-beb5-418eb2c6c033\",\"width\":1.4}";
                var near=new Vector3(1,.75f,0);var far=new Vector3(30,.75f,-20);var eye=new Vector3(0,1.65f,0);

                Reset(far);Sample(false,0);Sample(false,10);
                Check(File.Exists(path)&&!board.Placed&&!quest.Placing,"Untracked startup never forgets a saved board or forces placement");
                Sample(true,11);Sample(true,11.5f);
                Check(File.Exists(path),"A distant initial pose must settle before forgetting the board");
                // The first apparently distant pose was only the provisional tracking origin.
                camera.transform.position=far+new Vector3(-1,.9f,0);Sample(true,11.6f);Sample(true,12.2f);
                Check(File.Exists(path)&&!board.Placed,"Late correction of the head restarts the settling interval");
                Sample(true,12.4f);
                Check(board.Placed&&File.ReadAllText(path)==saved,"Nearby restored board survives head localization and keeps its saved data");

                Reset(far);Sample(true,0);Sample(true,.8f);
                Check(!File.Exists(path)&&!board.Placed&&!quest.Placing&&!session.Running&&board.transform.parent==null,
                    "Settled distant board is forgotten without starting a match or forcing setup at launch");
                Check(session.Message.Contains("Place it again"),"Forgotten placement explains that a new location is needed");
                quest.RequestStartMatch();
                Check(quest.StartPending&&quest.Placing&&!session.Running,"Start Match requests placement after forgetting the old location");
                quest.PlaceRay(new Ray(new Vector3(0,2,0),Vector3.down),0,Vector2.zero,true);
                Check(session.Running&&board.Placed&&!quest.StartPending&&!quest.Placing,"Confirming the new location starts the requested match");

                Reset(far);quest.RequestStartMatch();
                Check(quest.StartPending&&!quest.Placing&&!session.Running,"Early Start Match waits for restored-position validation");
                Sample(true,0);Sample(true,.8f);
                Check(!File.Exists(path)&&quest.Placing&&quest.StartPending&&!session.Running,"Pending start switches to fresh placement when the old board is distant");
                quest.CancelPlacement();
                Check(!quest.StartPending&&!quest.Placing&&!board.Placed&&!session.Running,"Cancelling new placement cannot resurrect the forgotten board");

                Reset(near);quest.RequestStartMatch();Sample(true,0);Sample(true,.8f);
                Check(session.Running&&board.Placed&&File.Exists(path)&&!quest.StartPending,"A nearby board starts the pending match without being placed again");
                camera.transform.position+=Vector3.right*20;Sample(true,2);Sample(true,3);
                Check(board.Placed&&File.Exists(path),"An active match never discards its board as the player moves");

                Reset(far);session.Pause("headset",true);Sample(true,0);Sample(true,3);
                Check(File.Exists(path),"Headset interruption cannot invalidate a saved location");
                session.Pause("headset",false);Sample(true,4);Sample(true,4.5f);
                Check(File.Exists(path),"Resumed tracking starts a new settling interval");
                Sample(true,4.8f);Check(!File.Exists(path),"Stable resumed tracking can forget a genuinely distant board");

                Reset(near);Sample(true,0);Sample(true,.8f);
                camera.transform.position=eye+Vector3.right*20;quest.RequestStartMatch();
                Check(quest.StartPending&&!session.Running,"A later head correction revalidates a previously accepted board before starting");
                Sample(true,2);Sample(true,2.8f);
                Check(!File.Exists(path)&&quest.Placing&&!session.Running,"Late localization cannot launch a match on the distant board");

                Reset(far);Sample(true,0);board.transform.position=near;Sample(true,.5f);Sample(true,1.3f);
                Check(File.Exists(path)&&board.Placed,"Late anchor localization near the viewer keeps the saved board");
                Reset(new Vector3(3,.75f,0));Sample(true,0);Sample(true,.8f);
                Check(File.Exists(path)&&board.Placed,"A board exactly three horizontal meters away is still retained");
                Reset(new Vector3(3.1f,.75f,0));Sample(true,0);Sample(true,.8f);
                Check(!File.Exists(path)&&!board.Placed,"A board beyond three horizontal meters is forgotten");
                Reset(new Vector3(1,0,0));Sample(true,0);Sample(true,.8f);
                Check(File.Exists(path)&&board.Placed,"A nearby floor board is retained despite normal head height");
                Reset(new Vector3(1,-8,0));Sample(true,0);Sample(true,.8f);
                Check(!File.Exists(path)&&!board.Placed,"A location on a different floor is also forgotten");

                Reset(far);quest.BeginPlacement();Sample(true,0);Sample(true,2);
                Check(File.Exists(path)&&quest.Placing,"Manual repositioning is never interrupted by the saved-board check");
                quest.CancelPlacement();Sample(true,3);Sample(true,3.8f);
                Check(!File.Exists(path)&&!board.Placed,"Cancelling manual repositioning still validates the restored location");
                Debug.Log($"FRONTIER_SAVED_BOARD passed: {checks} checks; synthetic tracking and temporary placement file, no physical Quest input");

                void Reset(Vector3 position)
                {
                    if(quest.Placing)quest.CancelPlacement();quest.CancelStartRequest();session.LeaveMatch();session.Pause("headset",false);
                    camera.transform.SetPositionAndRotation(eye,Quaternion.identity);
                    board.transform.SetParent(fixture.transform,true);board.transform.position=position;board.Width=1.4f;
                    File.WriteAllText(path,saved);Call(quest,"BeginSavedBoardCheck");
                }
                void Sample(bool tracked,float now)=>Call(quest,"CheckSavedBoard",tracked,now,path);
                void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;Debug.Log("SAVED_BOARD_PASS: "+message);}
            }
            finally
            {
                if(boardObject!=null)Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);
                if(camera!=null)Object.DestroyImmediate(camera.gameObject);if(File.Exists(path))File.Delete(path);
            }
        }
    }
}
