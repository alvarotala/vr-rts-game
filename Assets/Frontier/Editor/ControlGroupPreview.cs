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
    public static class ControlGroupPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
        private static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Private).Invoke(obj,args);

        public static void RunValidation()
        {
            Render();
            ActionWheelPreview.Render();
            QueuedInputChecks.Run();
            RallyPointInputChecks.Run();
            MenuFlowPreview.Render();
            HarvestingPreview.RenderHud();
        }

        [MenuItem("Frontier/Preview and check control groups")]
        public static void Render()
        {
            const string output="TestResults/ControlGroups";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Group input fixture");
            var console=new GameObject("Group console");var wheelObject=new GameObject("Group wheel");
            int checks=0,captures=0;float now=10;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=fixture.AddComponent<BoardView>();board.Session=session;Call(board,"Start");
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Call(panel,"Start");
                var wheel=wheelObject.AddComponent<ActionWheel>();wheel.Session=session;wheel.Panel=panel;wheel.Quest=quest;preview.AddSingleGO(wheelObject);
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Panel=panel;input.Quest=quest;input.Wheel=wheel;
                Ray ray=default;
                var directions=new[]{Vector2.up,Vector2.right,Vector2.down,Vector2.left};
                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();Refresh();Drain();
                    var workers=session.View.entities.Where(e=>e.owner==0&&session.Catalog[e.definition].role=="worker").Take(4).ToArray();
                    var hq=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                    ray=new Ray(board.Content.TransformPoint(new Vector3(workers[0].position.x,0,workers[0].position.y))+Vector3.up,Vector3.down);
                    var boardPose=board.transform.localToWorldMatrix;float width=board.Width;
                    for(int i=0;i<4;i++){session.Select(workers[i].id,false);session.Group(i,true);}
                    Check(Enumerable.Range(0,4).All(i=>session.GroupSize(i)==1),"All four independent groups can be saved");
                    session.Select(workers[0].id,false);Refresh();
                    Check(Field<Text[]>(panel,"groupCounts").Length==4&&Field<Text[]>(panel,"groupCounts").All(t=>t.text=="1 assigned"),"HUD displays all four group counts");
                    CapturePanel(faction+"-hud");

                    Check(Frame(press:true)&&wheel.IsOpen&&!wheel.InGroupMenu,"Single stick click opens actions immediately");
                    Frame();Frame(press:true);
                    Check(wheel.InGroupMenu&&wheel.Items.Count==4&&wheel.Highlighted==-1,"Double click switches to four groups without a default choice");
                    Frame();Frame(new Vector2(.15f,.12f));
                    Check(wheel.IsOpen&&session.Selection.SetEquals(new[]{workers[0].id}),"Centered stick and small drift do not choose a group");Capture(faction+"-neutral");
                    Frame(Vector2.right);Frame(Vector2.right,a:true,down:true,held:true);
                    Check(wheel.Highlighted==1&&wheel.IsOpen&&session.Selection.Contains(workers[0].id),"A and trigger do not confirm or select terrain while choosing groups");
                    Frame(Vector2.right);Capture(faction+"-recall");
                    Frame(new Vector2(.3f,0));Check(wheel.IsOpen,"Returning through the deadzone shoulder does not confirm early");
                    Frame();Check(!wheel.IsOpen&&session.Selection.SetEquals(new[]{workers[1].id}),"Centering confirms the highlighted group directly");
                    Check(Frame()&&!Frame(),"Closing frame and one release frame are consumed before terrain resumes");

                    for(int i=0;i<4;i++)
                    {
                        OpenGroups();Frame(directions[i]);
                        Check(wheel.Highlighted==i,"Cardinal direction selects group "+(i+1));
                        Frame();Check(session.Selection.SetEquals(new[]{workers[i].id}),"Release recalls group "+(i+1));Drain();
                    }
                    OpenGroups();Frame(Vector2.up);Frame(new Vector2(.71f,.70f));
                    Check(wheel.Highlighted==0,"Sector boundary jitter keeps the previous highlight");
                    Frame(Vector2.right);Frame(Vector2.down);Frame(Vector2.left);
                    Check(wheel.Highlighted==3&&session.Selection.Contains(workers[3].id),"Rotating across sectors only changes the preview");
                    Frame(b:true);Check(!wheel.IsOpen&&session.Selection.Contains(workers[3].id),"B cancels without recalling another group");Drain();

                    session.Select(workers[0].id,false);session.Select(workers[1].id,true);
                    OpenGroups();Frame(Vector2.left,grip:true,gripDown:true);
                    Check(wheel.IsOpen&&wheel.InGroupMenu&&wheel.GroupSaveMode,"Grip changes groups to save mode rather than closing the wheel");Capture(faction+"-save");
                    Frame(grip:true);
                    Check(!wheel.IsOpen&&session.GroupSize(3)==2&&session.Selection.Count==2,"Grip and stick release save the selection to group four");
                    Check(Frame(grip:true)&&Frame(),"Saving drains held grip and its release");Drain();
                    session.Select(hq.id,false);OpenGroups(grip:true);Frame(Vector2.left,grip:true);
                    Frame(Vector2.left);Check(!wheel.GroupSaveMode,"Releasing grip before centering returns to recall mode");
                    Frame();Check(session.Selection.SetEquals(new[]{workers[0].id,workers[1].id})&&session.GroupSize(3)==2,"Recall after releasing grip does not overwrite the group");Drain();

                    session.Select(hq.id,false);ClickGroup4(true);session.Select(0,false);ClickGroup4(false);
                    Check(session.Selection.SetEquals(new[]{hq.id})&&session.GroupSize(3)==1,"Fourth HUD button saves on hold and recalls on click");
                    session.Select(workers[0].id,false);session.ChooseBuild(session.Catalog.Role(faction,"supply").id);
                    OpenGroups();Frame(Vector2.left);Frame();
                    Check(session.Selection.Contains(hq.id)&&session.BuildDefinition==null,"Recalling a group clears an armed build mode without placing anything");Drain();

                    session.Select(0,false);OpenGroups();Frame(Vector2.right);Frame();
                    Check(session.Selection.SetEquals(new[]{workers[1].id}),"Groups can be recalled with no current selection");Drain();
                    OpenGroups();Frame(Vector2.down);Frame(Vector2.down,press:true);
                    Check(!wheel.IsOpen&&session.Selection.Contains(workers[1].id),"Stick click closes a group wheel without confirming");Drain();Frame(elapsed:.4f);
                    Frame(press:true);Frame(elapsed:.4f);Frame(press:true);
                    Check(!wheel.IsOpen,"Slow stick clicks toggle actions instead of opening groups");Drain();Frame(elapsed:.4f);
                    Frame(press:true);Frame();Frame(Vector2.right);Frame(Vector2.right,press:true);
                    Check(!wheel.IsOpen,"Using the action wheel direction interrupts a pending double click");Drain();Frame(elapsed:.4f);
                    Frame(press:true);Frame(elapsed:.4f);Frame(press:true);Frame();Frame(press:true);
                    Check(wheel.InGroupMenu,"Double stick also opens groups when actions were already open");Frame(b:true);Drain();

                    session.Select(workers[0].id,false);OpenGroups();Frame(Vector2.right);session.Pause("tracking",true);Frame();
                    Check(!wheel.IsOpen&&session.Selection.Contains(workers[0].id),"Tracking pause cancels without a release selection");session.Pause("tracking",false);Drain();
                    OpenGroups();Frame(Vector2.right);Call(input,"OnApplicationFocus",false);Frame();
                    Check(!wheel.IsOpen&&session.Selection.Contains(workers[0].id),"Focus loss cancels the highlighted group");Drain();
                    Frame(press:true);Call(input,"OnApplicationFocus",false);Drain();Frame(press:true);
                    Check(wheel.IsOpen&&!wheel.InGroupMenu,"Focus loss also clears a half-complete double stick gesture");Frame(b:true);Drain();
                    OpenGroups();Frame(Vector2.right);session.Select(hq.id,false);Frame();
                    Check(!wheel.IsOpen&&session.Selection.Contains(hq.id),"An external selection change invalidates group confirmation");Drain();

                    var dead=session.Match.Entities[workers[2].id];session.Match.Entities.Remove(dead.id);Refresh();
                    Check(session.GroupSize(2)==0,"Destroyed members no longer count toward a group");
                    OpenGroups();Frame(Vector2.down);Frame();
                    Check(session.Selection.Count==0&&session.Message.Contains("empty"),"Recalling an empty group is explicit and never restores dead units");Drain();
                    var enemy=session.Match.Entities.Values.First(e=>e.owner==1);
                    session.Selection.Add(enemy.id);session.Group(2,true);
                    Check(session.GroupSize(2)==0,"Groups never store forged enemy selection IDs");session.Selection.Clear();
                    Check(board.transform.localToWorldMatrix==boardPose&&board.Width==width,"Group navigation leaves the board fixed");
                    OpenGroups();Frame(Vector2.right);session.StartMatch();Frame();
                    Check(!wheel.IsOpen&&Enumerable.Range(0,4).All(i=>session.GroupSize(i)==0),"Rematch resets all four groups and cancels the old wheel");Drain();
                    OpenGroups();Frame(Vector2.right);session.LeaveMatch();Frame();
                    Check(!wheel.IsOpen&&session.Selection.Count==0,"Leaving the match cancels group selection");Drain();
                    Frame(press:true);Frame();Frame(press:true);
                    Check(!wheel.IsOpen,"Double stick is inactive in the main menu");Drain();
                }
                Debug.Log($"FRONTIER_CONTROL_GROUPS passed: {checks} checks, {captures} views; no player build");

                bool Frame(Vector2 stick=default,bool press=false,bool a=false,bool b=false,bool down=false,bool held=false,bool grip=false,bool gripDown=false,float elapsed=.05f)
                {
                    now+=elapsed;held|=down;grip|=gripDown;
                    bool used=(bool)Call(input,"RouteWheel",ray,stick,press,a,down,held,b,grip,gripDown,now);
                    if(!used&&panel.AcceptsGameInput)Call(input,"PointBoard",ray,down,false,a,grip);
                    return used;
                }
                void OpenGroups(bool grip=false){Drain();Frame(press:true,grip:grip);Frame(grip:grip);Frame(press:true,grip:grip);Check(wheel.InGroupMenu,"Double stick opens the group wheel");}
                void Drain(){Frame();Frame();}
                void Refresh(){typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Call(panel,"LateUpdate");}
                void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("CONTROL_GROUP_PASS: "+message);}
                void Capture(string name){HarvestingPreview.SaveCanvas(wheel.transform,Field<RectTransform>(wheel,"root"),output+"/"+name+".png",preview,900,1050);captures++;}
                void CapturePanel(string name){HarvestingPreview.SavePanel(panel,output+"/"+name+".png",preview);captures++;}
                void ClickGroup4(bool save)
                {
                    Refresh();var rect=panel.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Group 4"&&r.gameObject.activeInHierarchy);
                    var center=rect.TransformPoint(rect.rect.center);var pointer=new Ray(center-rect.forward,rect.forward);
                    Check(panel.Point(pointer,true,false),"Group 4 HUD press hits its target");
                    if(save)typeof(CommandPanel).GetField("pressedAt",Private).SetValue(panel,Time.unscaledTime-1);
                    Check(panel.Point(pointer,false,true),"Group 4 HUD release hits its target");Refresh();
                }
            }
            finally {Object.DestroyImmediate(wheelObject);Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();}
        }
    }
}
