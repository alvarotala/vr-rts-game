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
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    // Real console targets and input routing, with controlled hover time and controller edges.
    public static class ActionHelpPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
        private static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Private).SetValue(obj,value);
        private static void Invoke(object obj,string method)=>obj.GetType().GetMethod(method,Private).Invoke(obj,null);

        [MenuItem("Frontier/Preview and check action help")]
        public static void Render()
        {
            Directory.CreateDirectory("TestResults/ActionHelp");
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Action help fixture");var console=new GameObject("Action help console");
            const string placementKey="frontier.panel-placement.v1";
            bool savedPlacement=PlayerPrefs.HasKey(placementKey);string placement=PlayerPrefs.GetString(placementKey);
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.LoadMap("training");
                var board=fixture.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                preview.camera.transform.position=new Vector3(0,1.7f,-2);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Invoke(panel,"Start");
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Quest=quest;input.Panel=panel;
                var wheel=fixture.AddComponent<ActionWheel>();wheel.Session=session;wheel.Panel=panel;wheel.Quest=quest;input.Wheel=wheel;
                preview.AddSingleGO(console);Refresh();
                var offPanel=new Ray(new Vector3(100,100,100),Vector3.up);

                var newMatch=RayTo("New Match");
                Check(Frame(newMatch,aDown:true,aHeld:true)&&panel.Page==CommandPanel.MenuPage.Main,"A starts a menu press without firing early");
                Check(Frame(newMatch,aUp:true)&&panel.Page==CommandPanel.MenuPage.MatchSetup,"A release opens setup exactly like trigger");
                panel.Back();Refresh();

                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();session.Match.Players[0].minerals=5000;session.Match.Players[0].catalyst=5000;
                    var worker=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                    var hq=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                    var barracks=Add("barracks",1001);var tech=Add("tech",1002);
                    Select(worker.id);
                    var build=SelectionActions.For(session).Items.First(a=>a.Category==ActionCategory.Build);
                    var buildRay=RayTo(build.Title);
                    panel.Point(buildRay,false,false);AgeHover(1.49f);panel.Point(buildRay,false,false);
                    Check(!HelpVisible(),"Quick hover does not open help");
                    AgeHover(1.51f);panel.Point(buildRay,false,false);
                    Check(HelpVisible()&&Field<Text>(panel,"helpTitle").text==build.Label,"Sustained hover describes the pointed building");
                    Check(session.BuildDefinition==null&&session.Match.Buildings(0)==3,"Hover never selects construction or spends resources");
                    Capture(faction+"-building-help");
                    panel.Point(RayTo("Stop"),false,false);Check(!HelpVisible(),"Switching buttons restarts the delay");
                    AgeHover(2);panel.Point(RayTo("Stop"),false,false);Check(HelpVisible(),"Orders also explain their behavior");
                    panel.Point(offPanel,false,false);Check(!HelpVisible(),"Leaving actions restores the selection card");
                    CheckLayout();
                    var buildColor=Button(build.Title).GetComponent<Image>().color;
                    Check(buildColor!=Button("Stop").GetComponent<Image>().color,"Build and unit orders use different button colors");
                    Check(HasText("BUILD STRUCTURES")&&HasText("UNIT ORDERS"),"Categories also have readable headers independent of color");
                    CheckHelpForSelection(faction+"-build");
                    Hover(build.Title);panel.CancelPointerInteraction();Check(!HelpVisible(),"Cancelling pointer interaction hides stale help");
                    Hover(build.Title);panel.Back();Refresh();Check(!HelpVisible(),"Pause hides help");panel.Back();Refresh();
                    Hover(build.Title);session.Pause("tracking",true);Refresh();Check(!HelpVisible(),"Tracking pause hides help");session.Pause("tracking",false);
                    Select(hq.id);Check(!HelpVisible(),"Changing selection clears the previous tooltip");CheckHelpForSelection(faction+"-worker");
                    var workerHelp=SelectionActions.For(session).Items.Single(a=>a.Id=="train:"+worker.definition).Description;
                    Check(workerHelp.Contains("Stays on site")&&workerHelp.Contains("resume unfinished construction")&&workerHelp.Contains("repair damaged allies"),
                        "Worker help explains construction commitment and repair for "+faction);
                    Select(barracks.id);CheckHelpForSelection(faction+"-army");
                    Select(tech.id);CheckHelpForSelection(faction+"-tech");

                    Select(hq.id);string train="Train "+session.Catalog[worker.definition].name;
                    var trainRay=RayTo(train);int minerals=session.View.minerals;
                    Check(Frame(trainRay,aDown:true,aHeld:true)&&hq.queue.Count==0,"Holding A does not repeat production");
                    Frame(trainRay,aHeld:true);Frame(trainRay,aUp:true);
                    Check(hq.queue.Count==1&&session.View.minerals==minerals-session.Catalog[worker.definition].minerals,"A queues one unit and pays the actual cost");Refresh();
                    trainRay=RayTo(train);Frame(trainRay,aDown:true,aHeld:true);Frame(offPanel,aUp:true);
                    Check(hq.queue.Count==1,"Releasing A off the button cancels its click");
                    Check(!Frame(offPanel),"Capture ends after A releases away from the panel");
                    trainRay=RayTo(train);Frame(trainRay,aDown:true,aHeld:true);Frame(trainRay,down:true,held:true,aHeld:true);Frame(trainRay,up:true,aHeld:true);
                    Check(hq.queue.Count==1,"Trigger cannot prematurely release an A-owned press");Frame(trainRay,aUp:true);
                    Check(hq.queue.Count==2,"Combined aliases execute only once");Refresh();
                    trainRay=RayTo(train);Frame(trainRay,down:true,held:true);Frame(trainRay,aDown:true,aHeld:true,held:true);Frame(trainRay,up:true,aHeld:true);
                    Check(hq.queue.Count==3&&Frame(offPanel,aHeld:true)&&Frame(offPanel,aUp:true),"After trigger confirmation, the remaining A hold and release are consumed");
                    Check(!Frame(offPanel),"Normal map routing resumes after both aliases release");Refresh();
                    trainRay=RayTo(train);Frame(trainRay,aDown:true,aHeld:true);Select(worker.id);Frame(RayTo("Attack Move"),aUp:true);
                    Check(!session.AttackMoveMode,"Selection changes cannot redirect a held menu press to another action");
                    Frame(RayTo("Attack Move"),aDown:true,aHeld:true);Frame(RayTo("Attack Move"),aUp:true);
                    Check(session.AttackMoveMode&&worker.order!=CommandKind.AttackMove,"A arms Attack Move without sending its press through to the map");session.CancelMode();
                    var groupRay=RayTo("Group 1");Frame(groupRay,aDown:true,aHeld:true);Set(panel,"pressedAt",Time.unscaledTime-1);Frame(groupRay,aUp:true);
                    Check(session.GroupSize(0)==1,"Holding A saves a control group like trigger");session.Select(0,false);Refresh();
                    groupRay=RayTo("Group 1");Frame(groupRay,aDown:true,aHeld:true);Frame(groupRay,aUp:true);
                    Check(session.Selection.SetEquals(new[]{worker.id}),"Tapping A recalls a control group");Refresh();
                    var moveRay=RayTo("Move menu");Frame(moveRay,aDown:true,aHeld:true);Check(panel.IsDragging,"A can grab the menu handle");
                    Frame(moveRay,aUp:true);Check(!panel.IsDragging,"Releasing A releases the menu handle");
                    Hover(build.Title);var focus=typeof(CommanderInput).GetMethod("OnApplicationFocus",Private);focus.Invoke(input,new object[]{false});
                    Check(!HelpVisible()&&Frame(offPanel,aHeld:true)&&Frame(offPanel,aUp:true),"Focus loss cancels hover and consumes a held A");Frame(offPanel);

                    Select(tech.id);Frame(RayTo("Firepower I"),aDown:true,aHeld:true);Frame(RayTo("Firepower I"),aUp:true);Refresh();
                    Check(tech.queue.Count==1&&tech.queue[0].research,"A starts research through the shared command path");
                    CheckLayout();Hover("Firepower I");Capture(faction+"-grouped-research");
                    Check(HasText("TRAIN UNITS")&&HasText("RESEARCH")&&HasText("CANCEL / REFUND"),"Technology actions separate training, research and cancellation");
                    Frame(RayTo("Cancel / refund"),aDown:true,aHeld:true);Check(!HelpVisible(),"Pressing an action dismisses help");Frame(RayTo("Cancel / refund"),aUp:true);
                    Check(tech.queue.Count==0,"A cancels and refunds a queued item");Refresh();

                    // An A press that begins on the map is still a contextual order, never a menu click.
                    Select(worker.id);var destination=new Point(12,12);var mapRay=new Ray(board.Content.TransformPoint(new Vector3(destination.x,0,destination.y))+Vector3.up,Vector3.down);
                    Check(!Frame(mapRay,aDown:true,aHeld:true),"A on terrain is not captured by the menu");
                    typeof(CommanderInput).GetMethod("PointBoard",Private).Invoke(input,new object[]{mapRay,false,false,true,false});
                    Check(worker.order==CommandKind.Move,"Outside menus A still issues contextual movement");
                    Frame(RayTo("Hold Position"),aUp:true);Check(worker.order==CommandKind.Move,"An A release arriving from the map does not click a menu action");
                    Hover("Hold Position");session.LeaveMatch();Refresh();Check(!HelpVisible()&&panel.Page==CommandPanel.MenuPage.Main,"Leaving the match removes action help");

                    EntityState Add(string role,int id)
                    {
                        var type=session.Catalog.Role(faction,role);var entity=new EntityState{id=id,owner=0,definition=type.id,health=type.health,position=new Point(22,role=="tech"?17:12)};
                        session.Match.Entities.Add(id,entity);return entity;
                    }
                }
                Debug.Log($"FRONTIER_ACTION_HELP passed: {checks} checks, {captures} panels rendered; no player build");

                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("ACTION_HELP_PASS: "+message);}
                void Refresh()
                {
                    if(session.Running)typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));
                    Set(panel,"nextText",0f);Invoke(panel,"LateUpdate");
                }
                void Select(int id){session.Select(id,false);Refresh();}
                RectTransform Button(string name)=>panel.GetComponentsInChildren<RectTransform>().First(t=>t.name==name&&t.gameObject.activeInHierarchy);
                Ray RayTo(string name){var rect=Button(name);return new Ray(rect.TransformPoint(rect.rect.center)-rect.forward,rect.forward);}
                bool Frame(Ray ray,bool down=false,bool up=false,bool held=false,bool aDown=false,bool aUp=false,bool aHeld=false)
                    =>(bool)typeof(CommanderInput).GetMethod("RoutePanel",Private).Invoke(input,new object[]{ray,down,up,held,aDown,aUp,aHeld,0f});
                bool HelpVisible()=>Field<RectTransform>(panel,"actionHelp").gameObject.activeSelf;
                void AgeHover(float age)=>Set(panel,"hoverStartedAt",Time.unscaledTime-age);
                void Hover(string name){var ray=RayTo(name);panel.Point(ray,false,false);AgeHover(2);panel.Point(ray,false,false);}
                bool HasText(string text)=>panel.GetComponentsInChildren<Text>().Any(t=>t.text==text);
                void CheckHelpForSelection(string prefix)
                {
                    foreach(var action in SelectionActions.For(session).Items)
                    {
                        Hover(action.Title);
                        Check(HelpVisible()&&Field<Text>(panel,"helpDescription").text==action.Description&&action.Description.Length>30,"Descriptive help for "+action.Id);
                        Capture(prefix+"-"+action.Id.Replace(':','-'));
                    }
                    panel.CancelPointerInteraction();
                }
                void CheckLayout()
                {
                    var area=Field<RectTransform>(panel,"actions");
                    var buttons=area.Cast<RectTransform>().Where(t=>t.GetComponent<Image>()!=null).ToArray();
                    foreach(var b in buttons)
                        Check(b.anchoredPosition.x>=0&&b.anchoredPosition.x+b.rect.width<=area.rect.width&&-b.anchoredPosition.y+b.rect.height<=area.rect.height,"Action stays within its section: "+b.name);
                    Check(buttons.SelectMany((b,i)=>buttons.Skip(i+1).Select(other=>new Rect(b.anchoredPosition-new Vector2(0,b.rect.height),b.rect.size).Overlaps(new Rect(other.anchoredPosition-new Vector2(0,other.rect.height),other.rect.size)))).All(overlap=>!overlap),"Action buttons do not overlap");
                }
                void Capture(string name){HarvestingPreview.SavePanel(panel,"TestResults/ActionHelp/"+name+".png",preview);captures++;}
            }
            finally
            {
                Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();
                if(savedPlacement)PlayerPrefs.SetString(placementKey,placement);else PlayerPrefs.DeleteKey(placementKey);PlayerPrefs.Save();
            }
            ProductionQueuePreview.Render();
            ActionWheelPreview.Render();
        }
    }
}
