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
                Check(Has("New Match")&&Has("Settings")&&Has("Hotkeys")&&!Has("START MATCH"),"Main menu has dedicated navigation");
                Capture("01-main");
                Click("Settings");Check(panel.Page==CommandPanel.MenuPage.Settings&&!session.IsPaused,"Settings work before a match without creating a pause");
                Check(Has("Hand: Right")&&Field<Text>(panel,"selection").text.Contains("Right controller active"),"Settings identify the sole active hand");
                Click("Hand: Right");Check(Has("Hand: Left")&&quest.ActiveController==OVRInput.Controller.LTouch,"Settings switches to the left hand");
                Click("Hand: Left");Capture("02-settings");
                Click("Back");Check(panel.Page==CommandPanel.MenuPage.Main,"Settings return to main menu");
                Click("Hotkeys");
                Check(panel.Page==CommandPanel.MenuPage.Hotkeys&&!Field<bool>(panel,"hotkeysTouch"),"Dedicated Hotkeys page defaults to the connected desktop input");
                Check(!Field<RectTransform>(panel,"controlGuide").gameObject.activeInHierarchy,"Hotkeys is a separate reference, not the pause tutorial cards");
                Check(!Has("Quest Touch")&&!Has("Mouse / Keyboard"),"Hotkeys exposes no inactive-device tabs");
                BrowseHotkeys(false);typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);Invoke(panel,"Rebuild");BrowseHotkeys(true);
                Check(Field<Text>(panel,"hotkeysDevice").text.Contains("Right Touch"),"Hotkeys identifies the selected Touch controller");
                Click("Hotkeys: Groups");Click("Next");typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);Invoke(panel,"Rebuild");
                Check(Field<int>(panel,"hotkeysPage")==0,"Switching device resets pagination");
                Check(!session.Running&&!session.IsPaused&&!panel.AcceptsGameInput,"Browsing Hotkeys before a match never starts a match or enables game input");
                ExportDefaults();
                panel.Back();Refresh();Click("New Match");
                Check(panel.Page==CommandPanel.MenuPage.MatchSetup&&board.Content.gameObject.activeSelf,"New Match opens setup and map preview");
                Click("AI: Normal");Check(session.Difficulty=="very-easy"&&Has("AI: Very Easy")&&Field<Text>(panel,"harvesting").text.Contains("12 min"),"Setup offers Very Easy with a practice description");Capture("05-very-easy");
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
                Click("Pause");Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"Expanded Quest pause button works with the pointer");
                Check(Field<RectTransform>(panel,"controlGuide").gameObject.activeInHierarchy&&Has("Help: Selection")&&Has("Resume match"),"Pause shows a control-help section alongside match navigation");
                int pausedTick=session.Match.Tick;var beforeSelection=session.Selection.ToArray();int minerals=session.View.minerals;
                foreach(string topic in new[]{"Selection","Orders","Menus","Groups","Production","Board"})
                {
                    Click("Help: "+topic);for(int i=0;i<3;i++)session.Match.Step();
                    Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused&&session.Match.Tick==pausedTick&&!panel.AcceptsGameInput,"Browsing "+topic+" keeps simulation paused and gameplay input blocked");
                    Capture("07-pause-touch-"+topic.ToLowerInvariant());
                }
                Check(session.Selection.SetEquals(beforeSelection)&&session.View.minerals==minerals,"Guide cards never change selection or resources");
                Click("Resume match");Check(!Field<RectTransform>(panel,"controlGuide").gameObject.activeInHierarchy,"Resuming hides the guide and removes its targets");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);Invoke(panel,"Rebuild");Refresh();
                Check(Has("Full map")&&Has("Pause"),"Desktop retains its development view controls");
                var match=session.Match;var worker=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                session.Select(worker.id,false);session.Group(0,true);session.ChooseBuild(session.Catalog.Role(session.Faction,"supply").id);
                Invoke(board,"LateUpdate");Refresh();Capture("06-actions");
                Click("Pause");int tick=match.Tick;for(int i=0;i<30;i++)match.Step();
                Check(session.IsPaused&&match.Tick==tick&&!panel.AcceptsGameInput,"Pause stops simulation and blocks game input");Capture("07-pause");
                Click("Settings");Click("Back");Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"In-match settings return to pause");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                Click("Hotkeys");Check(Field<bool>(panel,"hotkeysTouch"),"Opening from Quest pause defaults to Touch even after desktop preview");
                int hotkeysTick=match.Tick;var hotkeysSelection=session.Selection.ToArray();int hotkeysMinerals=session.View.minerals;string buildMode=session.BuildDefinition;
                Click("Hotkeys: Groups");Click("Next");Click("Previous");
                for(int i=0;i<30;i++)match.Step();
                var row=Field<RectTransform[]>(panel,"hotkeyRows")[0];var rowCenter=row.TransformPoint(row.rect.center);var rowRay=new Ray(rowCenter-row.forward,row.forward);
                panel.Point(rowRay,true,false);panel.Point(rowRay,false,true);Refresh();
                Check(match.Tick==hotkeysTick&&session.IsPaused&&!panel.AcceptsGameInput&&session.Selection.SetEquals(hotkeysSelection)&&session.View.minerals==hotkeysMinerals&&session.BuildDefinition==buildMode&&session.GroupSize(0)==1,"Hotkey browsing and clicking read-only rows preserve pause, selection, resources, build mode and groups");
                Capture("07-paused-hotkeys");panel.Back();Refresh();
                Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused&&!Field<RectTransform>(panel,"hotkeys").gameObject.activeInHierarchy,"B / Back returns Hotkeys to pause and hides all reference targets");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);
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
                string GuideText()=>string.Join("\n",Field<RectTransform>(panel,"controlGuide").GetComponentsInChildren<Text>().Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.text));
                void Click(string name)
                {
                    Refresh();var button=panel.GetComponentsInChildren<RectTransform>().FirstOrDefault(t=>t.gameObject.activeInHierarchy&&t.name==name);
                    if(button==null)throw new Exception("Menu button missing: "+name);
                    var center=button.TransformPoint(button.rect.center);var ray=new Ray(center-button.forward,button.forward);
                    if(!panel.Point(ray,true,false)||!panel.Point(ray,false,true))throw new Exception("Menu ray did not hit: "+name);
                    Refresh();
                }
                void BrowseHotkeys(bool touch)
                {
                    var ids=new System.Collections.Generic.HashSet<string>();
                    var topics=DefaultControlCatalog.For(touch);
                    foreach(var topic in topics)
                    {
                        Click("Hotkeys: "+topic.Name);int offset=0;
                        do
                        {
                            var rows=Field<RectTransform[]>(panel,"hotkeyRows").Where(r=>r.gameObject.activeInHierarchy).ToArray();
                            Check(rows.Length==Mathf.Min(5,topic.Commands.Count-offset),"Hotkeys paginates "+topic.Name+" without missing rows");
                            for(int i=0;i<rows.Length;i++)
                            {
                                var command=topic.Commands[offset+i];
                                Check(rows[i].name=="Hotkey: "+command.Id&&ids.Add(command.Id),"Unique stable command visible: "+command.Id);
                                string text=string.Join("\n",rows[i].GetComponentsInChildren<Text>().Select(t=>t.text));
                                Check(text.Contains(command.Name)&&text.Contains(command.Gesture)&&text.Contains(command.Description),"Binding and context are visible: "+command.Id);
                            }
                            Capture("hotkeys-"+(touch?"touch":"desktop")+"-"+topic.Name.ToLowerInvariant()+"-"+(offset/5+1));
                            offset+=rows.Length;
                            if(!Has("Next"))break;
                            Click("Next");
                        }while(true);
                        Check(offset==topic.Commands.Count,"Every "+topic.Name+" command is reachable by pagination");
                        Click("Previous");Check(!Has("Previous")&&Has("Next"),"Previous returns to the first page of "+topic.Name);
                    }
                    Check(ids.Count==topics.Sum(t=>t.Commands.Count),"No duplicate command identifiers within the device catalog");
                }
                void ExportDefaults()
                {
                    var text=new System.Text.StringBuilder("# Default controls\n\nRead-only reference shared by Hotkeys and the pause guide. Catalog schema: "+DefaultControlCatalog.SchemaVersion+".\n");
                    foreach(bool touch in new[]{true,false})
                    {
                        text.Append("\n## "+(touch?"Quest 3 - right Touch controller":"Desktop - mouse and keyboard")+"\n");
                        foreach(var topic in DefaultControlCatalog.For(touch))
                        {
                            text.Append("\n### "+topic.Name+"\n\n| Command | Default input / menu action | Context |\n|---|---|---|\n");
                            foreach(var command in topic.Commands)text.Append("| "+command.Name+" | "+command.Gesture+" | "+command.Description+" |\n");
                        }
                    }
                    File.WriteAllText("TestResults/Menus/default-controls.md",text.ToString());
                }
                void Capture(string name)
                {
                    Refresh();var guide=Field<RectTransform>(panel,"controlGuide");
                    if(guide.gameObject.activeInHierarchy)
                    {
                        Canvas.ForceUpdateCanvases();
                        foreach(var text in guide.GetComponentsInChildren<Text>())
                            if(text.preferredHeight>text.rectTransform.rect.height+.5f)throw new Exception("Clipped guide text: "+text.text);
                        var actions=Field<RectTransform>(panel,"actions");
                        float guideTop=-guide.anchoredPosition.y,guideBottom=guideTop+guide.sizeDelta.y;
                        float actionsTop=-actions.anchoredPosition.y,actionsBottom=actionsTop+actions.sizeDelta.y;
                        if(!(actionsBottom<=guideTop||guideBottom<=actionsTop))throw new Exception("Control guide overlaps navigation buttons");
                        if(guideBottom>-Field<Text>(panel,"status").rectTransform.anchoredPosition.y)throw new Exception("Control guide overlaps footer");
                    }
                    var reference=Field<RectTransform>(panel,"hotkeys");
                    if(reference.gameObject.activeInHierarchy)
                    {
                        Canvas.ForceUpdateCanvases();
                        foreach(var text in reference.GetComponentsInChildren<Text>())
                            if(text.preferredHeight>text.rectTransform.rect.height+.5f)throw new Exception("Clipped hotkey text: "+text.text);
                        var actions=Field<RectTransform>(panel,"actions");
                        if(-reference.anchoredPosition.y+reference.sizeDelta.y>-actions.anchoredPosition.y)throw new Exception("Hotkeys overlaps navigation");
                        if(-actions.anchoredPosition.y+actions.sizeDelta.y>-Field<Text>(panel,"status").rectTransform.anchoredPosition.y)throw new Exception("Hotkey navigation overlaps footer");
                    }
                    HarvestingPreview.SavePanel(panel,"TestResults/Menus/"+name+".png",preview);captures++;
                }
            }
            finally
            {
                Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();
            }
        }
    }
}
