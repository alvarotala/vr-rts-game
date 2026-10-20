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
        public static void Run(){Render();MenuFlowPreview.Render();ComfortAudioPreview.Render();}

        [MenuItem("Frontier/Preview startup menu position")]
        public static void Render()
        {
            const string key="frontier.panel-placement.v1",output="TestResults/StartupMenu";
            bool had=PlayerPrefs.HasKey(key);string saved=PlayerPrefs.GetString(key,"");
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Startup menu fixture");
            GameObject console=null;Camera createdCamera=null;int checks=0;
            try
            {
                Directory.CreateDirectory(output);
                const string profile="{\"offset\":{\"x\":0.45,\"y\":0.9,\"z\":-0.6}}";
                PlayerPrefs.SetString(key,profile);
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
                Check(!root.gameObject.activeSelf&&!panel.Point(new Ray(Vector3.zero,Vector3.forward),true,false),"Startup waits for a tracked head pose and cannot click the hidden panel");
                eye.SetPositionAndRotation(new Vector3(8,1.65f,-6),Quaternion.Euler(8,132,0));Refresh();CheckInFront();
                Check(panel.Page==CommandPanel.MenuPage.Main&&!session.Running&&!quest.Placing&&!board.Content.gameObject.activeSelf,"Startup offers main menu without forcing board placement");
                Check(board.transform.localToWorldMatrix==boardPose&&PlayerPrefs.GetString(key)==profile,"Startup preserves the restored board and saved match-console offset");
                Capture("01-restored-board-start");

                var menuPosition=root.position;var menuScale=root.localScale;
                anchor.SetPositionAndRotation(new Vector3(25,.9f,-17),Quaternion.Euler(0,-87,0));board.Width=.65f;board.Fit();Refresh();
                Check(root.position==menuPosition&&root.localScale==menuScale,"Late spatial-anchor restoration and saved board size cannot move or resize the startup menu");
                eye.position+=new Vector3(.3f,.1f,-.2f);eye.rotation=Quaternion.Euler(0,155,0);Refresh();
                Check(root.position==menuPosition,"Looking or walking around does not attach the menu to the head");
                Click("Settings");Check(root.position==menuPosition,"Settings stays beside the startup menu rather than jumping to the old board");
                var previousBoard=board.transform.localToWorldMatrix;var previousParent=board.transform.parent;float previousWidth=board.Width;
                Click("Reposition board");Check(quest.Placing&&root.position==menuPosition,"Reposition board is reachable before a match from the nearby menu");
                quest.PlaceRay(new Ray(eye.position+Vector3.up,Vector3.down),0,Vector2.zero,false);Refresh();
                Check(root.position==menuPosition,"The setup console stays still while aiming a new board position");
                quest.CancelPlacement();Refresh();
                Check(board.transform.localToWorldMatrix==previousBoard&&board.transform.parent==previousParent&&board.Width==previousWidth,"Cancel preserves the old anchored board without moving the menu");
                Click("Back");

                var grab=RayTo("Move menu");panel.Point(grab,true,false);
                Check(panel.IsDragging,"Startup menu can be grabbed near the viewer");
                panel.Point(new Ray(grab.origin+root.right*.25f,grab.direction),false,true);Refresh();
                Check(!panel.IsDragging&&Vector3.Distance(root.position,menuPosition)>.2f&&Vector3.Distance(root.position,eye.position)<2,"Dragging startup UI stays near the viewer even when the board is far away");
                Check(PlayerPrefs.GetString(key)==profile,"Dragging startup UI does not overwrite match-console placement");
                Click("Reset menu");CheckInFront();Check(PlayerPrefs.GetString(key)==profile,"Reset position recenters startup UI without deleting saved match placement");

                Call(panel,"OnApplicationFocus",false);eye.SetPositionAndRotation(new Vector3(-4,1.4f,6),Quaternion.Euler(0,-70,0));Call(panel,"OnApplicationFocus",true);Refresh();CheckInFront();
                Capture("02-returned-from-another-position");
                session.Pause("headset",true);Refresh();Check(!root.gameObject.activeSelf,"Headset interruption hides setup UI until tracking is usable");
                eye.SetPositionAndRotation(new Vector3(14,1.7f,3),Quaternion.Euler(0,22,0));session.Pause("headset",false);Refresh();CheckInFront();
                Call(panel,"OnApplicationPause",true);eye.rotation=Quaternion.Euler(0,-125,0);Call(panel,"OnApplicationPause",false);Refresh();CheckInFront();

                session.StartMatch();Refresh();
                var expected=board.transform.TransformPoint(new Vector3(.45f*board.Width,.9f,-.6f*board.Depth));
                Check(panel.Page==CommandPanel.MenuPage.Actions&&Vector3.Distance(root.position,expected)<.0001f,"Starting a match restores the existing board-relative console placement");
                panel.ToggleMenu();Call(panel,"OnApplicationFocus",true);Refresh();
                Check(session.IsPaused&&Vector3.Distance(root.position,expected)<.0001f,"Focus recovery during a match leaves its pause console anchored to the board");
                session.LeaveMatch();Refresh();CheckInFront();
                Object.DestroyImmediate(console);eye.SetPositionAndRotation(new Vector3(-9,1.55f,-15),Quaternion.Euler(0,75,0));
                panel=CreatePanel();root=Field<RectTransform>(panel,"root");Refresh();CheckInFront();
                Check(PlayerPrefs.GetString(key)==profile,"A fresh app console ignores the previous world position without erasing saved data");
                Capture("03-fresh-start-new-position");
                board.Placed=false;Click("New Match");menuPosition=root.position;Click("START MATCH");
                Check(quest.StartPending&&quest.Placing&&!session.Running&&root.position==menuPosition,"With no saved board, placement is still deferred until Start Match and setup stays nearby");
                quest.CancelPlacement();Refresh();
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);Refresh();
                Check(Vector3.Distance(root.position,expected)<.0001f,"Desktop retains board-relative menu placement");
                Debug.Log($"FRONTIER_STARTUP_MENU passed: {checks} checks, 3 views; synthetic head poses, no physical Quest input");

                CommandPanel CreatePanel()
                {
                    console=new GameObject("Startup console");preview.AddSingleGO(console);
                    var created=console.AddComponent<CommandPanel>();created.Session=session;created.Board=board;created.Quest=quest;created.PlaceRequested=quest.BeginPlacement;Call(created,"Start");return created;
                }
                void Refresh(){Call(panel,"LateUpdate");}
                void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;Debug.Log("STARTUP_MENU_PASS: "+message);}
                void CheckInFront()
                {
                    var facing=Vector3.ProjectOnPlane(eye.forward,Vector3.up).normalized;
                    Check(root.gameObject.activeSelf&&Vector3.Distance(root.position,eye.position+facing*1.25f+Vector3.down*.12f)<.0001f&&Vector3.Dot(root.forward,(root.position-eye.position).normalized)>.999f,
                        "Main/setup menu appears 1.25 m in front of the current viewer and faces them");
                }
                Ray RayTo(string name)
                {
                    var target=panel.GetComponentsInChildren<RectTransform>().Single(r=>r.name==name&&r.gameObject.activeInHierarchy);
                    var center=target.TransformPoint(target.rect.center);return new Ray(center-target.forward*.6f,target.forward);
                }
                void Click(string name){Refresh();var ray=RayTo(name);Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Can click "+name);Refresh();}
                void Capture(string name)
                {
                    preview.camera.orthographic=false;preview.camera.fieldOfView=60;preview.camera.nearClipPlane=.02f;preview.camera.farClipPlane=60;
                    HarvestingPreview.SaveCanvas(panel.transform,root,output+"/"+name+".png",preview,1400,900,preservePose:true);
                }
            }
            finally
            {
                if(console!=null)Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);
                if(createdCamera!=null)Object.DestroyImmediate(createdCamera.gameObject);preview.Cleanup();
                if(had)PlayerPrefs.SetString(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
            }
        }
    }
}
