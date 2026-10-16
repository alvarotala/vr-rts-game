using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    // Exercises the actual ray-driven menu and session lifecycle without building a player.
    public static class MenuFlowPreview
    {
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);

        [MenuItem("Frontier/Preview and check menu flow")]
        public static void Render()
        {
            Directory.CreateDirectory("TestResults/Menus");
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Menu flow fixture");var console=new GameObject("Menu flow console");
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");
                var boardObject=new GameObject("Preview board");boardObject.transform.SetParent(fixture.transform);
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                preview.camera.transform.position=new Vector3(0,1.7f,-2);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;panel.PlaceRequested=quest.BeginPlacement;
                Invoke(panel,"Start");preview.AddSingleGO(console);Refresh();
                Check(panel.Page==CommandPanel.MenuPage.Main&&!session.Running&&session.Match==null,"Startup opens main menu without a match");
                Check(!board.Content.gameObject.activeSelf&&!panel.AcceptsGameInput,"Main menu hides board and blocks game input");
                Check(Has("New Match")&&Has("Settings")&&Has("Controls")&&!Has("START MATCH"),"Main menu has dedicated navigation");
                Capture("01-main");
                Click("Settings");Check(panel.Page==CommandPanel.MenuPage.Settings&&!session.IsPaused,"Settings work before a match without creating a pause");
                Check(!Has("Hand: Left")&&!Has("Hand: Right")&&Field<Text>(panel,"selection").text.Contains("Right controller only"),"Settings describe right-only input without an obsolete handedness toggle");Capture("02-settings");
                Click("Back");Check(panel.Page==CommandPanel.MenuPage.Main,"Settings return to main menu");
                Click("Controls");Capture("03-controls-desktop");
                Check(Field<Text>(panel,"selection").text.Contains("Escape"),"Desktop controls are shown");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);Refresh();Capture("04-controls-touch");
                Check(Field<Text>(panel,"selection").text.Contains("A: move")&&Field<Text>(panel,"selection").text.Contains("Hold right grip: add to selection"),"Touch controls use A and retain the right grip modifier");
                var touchControls=Field<Text>(panel,"selection").text;
                Check(touchControls.Contains("Board stays fixed")&&!touchControls.Contains("zoom")&&!touchControls.Contains("full map")&&!touchControls.Contains("stick: pan"),"Touch help explains fixed placement without obsolete navigation controls");
                Check(touchControls.Contains("Right stick: adjust distance")&&touchControls.Contains("Grip + right stick press: recall menu"),"Touch menu movement and recall remain documented");
                Check(touchControls.Contains("Right stick: press for action wheel")&&touchControls.Contains("Left controller: unassigned"),"Help describes the right-hand wheel and unused left controller");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);
                panel.Back();Refresh();Click("New Match");
                Check(panel.Page==CommandPanel.MenuPage.MatchSetup&&board.Content.gameObject.activeSelf,"New Match opens setup and map preview");
                Click("AI: Normal");Check(session.Difficulty=="very-easy"&&Has("AI: Very Easy")&&Field<Text>(panel,"harvesting").text.Contains("18 min"),"Setup offers Very Easy with a practice description");Capture("05-very-easy");
                Click("AI: Very Easy");Check(session.Difficulty=="easy"&&Has("AI: Easy"),"Very Easy cycles to Easy");
                Click("AI: Easy");Check(session.Difficulty=="normal"&&Has("AI: Normal"),"Easy cycles to Normal");Click("AI: Normal");
                Click("START MATCH");Check(!session.Running&&session.Message.Contains("placement"),"Starting requires confirmed board placement");
                board.Placed=true;var placedPosition=new Vector3(.2f,.75f,.1f);board.transform.position=placedPosition;
                var placedRotation=Quaternion.Euler(0,25,0);board.transform.rotation=placedRotation;float placedWidth=board.Width;
                Click("Place board");Check(quest.Placing&&!board.Placed,"Placement starts from setup");
                board.transform.position+=Vector3.right;board.Width=2;
                panel.Back();Refresh();
                Check(!quest.Placing&&board.Placed&&!session.IsPaused,"Cancel placement restores readiness and removes only placement pause");
                Check(board.transform.parent==fixture.transform&&board.transform.position==placedPosition&&board.transform.rotation==placedRotation&&board.Width==placedWidth,"Cancel placement restores the board transform and parent");
                Capture("05-setup");
                session.Pause("tracking",true);Click("START MATCH");
                Check(session.Running&&panel.Page==CommandPanel.MenuPage.Actions&&session.IsPaused&&!panel.AcceptsGameInput,"Starting preserves tracking pause");
                Check(Field<Strategist>(session,"bot").Difficulty==AiDifficulty.VeryEasy,"Starting instantiates the selected Very Easy AI");
                session.Pause("tracking",false);Refresh();
                Check(panel.AcceptsGameInput&&!Has("New Match")&&Has("Pause"),"Match exposes action panel instead of main menu");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);Invoke(panel,"Rebuild");Refresh();
                Check(!Has("Full map")&&Has("Pause"),"Quest HUD removes the obsolete map reset action");Capture("06-actions-touch");
                Click("Pause");Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"Expanded Quest pause button works with the pointer");Click("Resume match");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);Invoke(panel,"Rebuild");Refresh();
                Check(Has("Full map")&&Has("Pause"),"Desktop retains its development view controls");
                var match=session.Match;var worker=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                session.Select(worker.id,false);session.Group(0,true);session.ChooseBuild(session.Catalog.Role(session.Faction,"supply").id);
                Invoke(board,"LateUpdate");Refresh();Capture("06-actions");
                Click("Pause");int tick=match.Tick;for(int i=0;i<30;i++)match.Step();
                Check(session.IsPaused&&match.Tick==tick&&!panel.AcceptsGameInput,"Pause stops simulation and blocks game input");Capture("07-pause");
                Click("Settings");Click("Back");Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"In-match settings return to pause");
                Click("Controls");panel.Back();Refresh();Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"In-match controls return to pause");
                Click("Return to main menu");Check(panel.Page==CommandPanel.MenuPage.LeaveConfirmation&&session.Running,"Leaving an active match asks before losing progress");Capture("08-leave-confirmation");
                Click("Keep playing");Check(session.Match==match&&!session.IsPaused&&session.Selection.Contains(worker.id)&&session.GroupSize(0)==1,"Cancel leaving resumes the same match and selection");
                session.Pause("headset",true);panel.ToggleMenu();Refresh();Click("Resume match");
                Check(session.IsPaused&&!panel.AcceptsGameInput,"Resume does not clear headset pause");
                panel.ToggleMenu();Refresh();Click("Return to main menu");Click("Leave match");
                Check(panel.Page==CommandPanel.MenuPage.Main&&!session.Running&&session.Match==null&&session.View==null,"Leave returns to main and releases simulation and view");
                Check(session.Selection.Count==0&&session.SelectedResource==0&&session.GroupSize(0)==0&&session.BuildDefinition==null&&!session.AttackMoveMode,"Leave clears all match input state");
                Check(session.IsPaused&&match.Paused,"Leave preserves external pause and freezes old match");
                Check(!board.Content.gameObject.activeSelf&&Field<System.Collections.IDictionary>(board,"entities").Count==0&&Field<System.Collections.IDictionary>(board,"resources").Count==0,"Leave clears old world visuals and hides board");
                Check(board.Placed&&board.transform.position==placedPosition&&board.transform.rotation==placedRotation&&board.transform.parent==fixture.transform,"Leave retains physical board placement");Capture("09-returned-main");
                session.Pause("headset",false);Click("New Match");Click("Faction: Consortium");Click("Map: The Rift");
                Check(session.Faction=="biological"&&session.MapId=="training","A new setup can change faction and map");Click("START MATCH");
                Check(session.Running&&session.Match!=match&&session.Match.Tick==0&&session.Match.Winner==-1&&session.Selection.Count==0&&!session.IsPaused,"Next match starts clean");
                Check(session.View.entities.Any(e=>e.owner==0&&e.definition=="biological.hq"),"Next match uses the new faction");
                Check(Field<Strategist>(session,"bot").Difficulty==AiDifficulty.VeryEasy,"Next match retains selected difficulty");
                session.Send(CommandKind.Surrender);Refresh();
                Check(panel.Page==CommandPanel.MenuPage.Result&&Has("Rematch")&&Has("Main menu")&&!Has("Resume match"),"Finished match offers rematch or main menu only");Capture("10-result");
                var finished=session.Match;Click("Rematch");
                Check(session.Match!=finished&&session.Match.Winner==-1&&panel.Page==CommandPanel.MenuPage.Actions&&!session.IsPaused,"Rematch resets result and menu state");
                Check(Field<Strategist>(session,"bot").Difficulty==AiDifficulty.VeryEasy,"Rematch retains selected difficulty");
                session.Send(CommandKind.Surrender);Refresh();Click("Main menu");
                Check(panel.Page==CommandPanel.MenuPage.Main&&session.Match==null,"Result returns directly to main menu");
                // The same shortcut can enter and leave pause; pre-match shortcuts are harmless.
                panel.ToggleMenu();Check(panel.Page==CommandPanel.MenuPage.Main&&!session.IsPaused,"Pause shortcut is harmless in main menu");
                Debug.Log($"FRONTIER_MENU_FLOW passed: {checks} checks, {captures} panels rendered; no player build");

                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("MENU_PASS: "+message);}
                void Refresh(){typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Invoke(panel,"LateUpdate");}
                bool Has(string name)=>panel.GetComponentsInChildren<RectTransform>().Any(t=>t.gameObject.activeInHierarchy&&t.name==name);
                void Click(string name)
                {
                    Refresh();var button=panel.GetComponentsInChildren<RectTransform>().FirstOrDefault(t=>t.gameObject.activeInHierarchy&&t.name==name);
                    if(button==null)throw new Exception("Menu button missing: "+name);
                    var center=button.TransformPoint(button.rect.center);var ray=new Ray(center-button.forward,button.forward);
                    if(!panel.Point(ray,true,false)||!panel.Point(ray,false,true))throw new Exception("Menu ray did not hit: "+name);
                    Refresh();
                }
                void Capture(string name){Refresh();HarvestingPreview.SavePanel(panel,"TestResults/Menus/"+name+".png",preview);captures++;}
            }
            finally
            {
                Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();
            }
        }
    }
}
