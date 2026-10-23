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
        private static void Invoke(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);

        [MenuItem("Frontier/Preview and check menu flow")]
        public static void Render()
        {
            Directory.CreateDirectory("TestResults/Menus");
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Menu flow fixture");var console=new GameObject("Menu flow console");
            int checks=0,captures=0;
            bool hadBasePreference=PlayerPrefs.HasKey(BoardView.BaseTargetingKey);
            int basePreference=PlayerPrefs.GetInt(BoardView.BaseTargetingKey);
            bool hadFogPreference=PlayerPrefs.HasKey(BoardView.FogDensityKey);
            float fogPreference=PlayerPrefs.GetFloat(BoardView.FogDensityKey);
            bool hadLightingPreference=PlayerPrefs.HasKey(BoardView.LightingKey);
            int lightingPreference=PlayerPrefs.GetInt(BoardView.LightingKey);
            bool hadLanguagePreference=PlayerPrefs.HasKey(Localization.PreferenceKey);
            string languagePreference=PlayerPrefs.GetString(Localization.PreferenceKey);
            try
            {
                Localization.SetPreference("en");
                PlayerPrefs.DeleteKey(BoardView.BaseTargetingKey);
                PlayerPrefs.DeleteKey(BoardView.FogDensityKey);
                PlayerPrefs.DeleteKey(BoardView.LightingKey);
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
                Check(Has("Guided tutorial")&&Has("New Match")&&Has("Guides")&&Has("Settings")&&!Has("Hotkeys")&&!Has("START MATCH"),"Main menu has guided tutorial, match, guides and unified settings navigation");
                Capture("01-main");
                Click("Guides");
                Check(panel.Page==CommandPanel.MenuPage.Guides&&Has("Control guide")&&Has("Units")&&Has("Structures"),"Main menu opens all guide categories");Capture("01-guides");
                foreach(var entry in new[]{("Control guide",CommandPanel.MenuPage.Guide),("Units",CommandPanel.MenuPage.UnitsGuide),("Structures",CommandPanel.MenuPage.StructuresGuide)})
                {
                    Click(entry.Item1);
                    Check(panel.Page==entry.Item2&&!session.Running&&session.Match==null&&!panel.AcceptsGameInput,"Reading "+entry.Item1+" before a match leaves gameplay inactive");
                    Capture("01-guide-"+entry.Item1.ToLowerInvariant().Replace(" ","-"));
                    Click("Back");Check(panel.Page==CommandPanel.MenuPage.Guides,"Guide detail Back returns to its category hub");
                }
                Click("Back");Check(panel.Page==CommandPanel.MenuPage.Main,"Guides Back returns to the original main menu");
                Click("Settings");Check(panel.Page==CommandPanel.MenuPage.Settings&&!session.IsPaused,"Settings work before a match without creating a pause");
                Check(Has("Controls")&&Has("Audio")&&Has("Visuals")&&Has("Play space"),"Settings organizes controls, sound, visuals and board placement into tabs");
                Click("Controls");
                Check(Has("Hand: Right")&&Has("Hotkeys")&&Has("Selection: Bodies")&&quest.ActiveController==OVRInput.Controller.RTouch,"Controls identify the active hand and share targeting and hotkey settings");
                Click("Hand: Right");Check(Has("Hand: Left")&&quest.ActiveController==OVRInput.Controller.LTouch,"Settings switches to the left hand");
                Click("Hand: Left");Capture("02-settings");
                Click("Audio");
                Check(Has("Music: "+Mathf.RoundToInt(AudioFeedback.MusicVolume*100)+"%")&&Has("Effects: "+Mathf.RoundToInt(AudioFeedback.EffectsVolume*100)+"%")&&!Has("Hotkeys"),"Audio tab exposes current volume settings without unrelated controls");Capture("02-settings-audio");
                Click("Visuals");
                Check(board.FogDensity==1&&Has("Fog: 0% (Off)")&&Has("Fog: 25%")&&Has("Fog: 50%")&&Has("Fog: 75%")&&Has("Fog: 100%"),"Visual settings offer the original fog density and an explicit Off preset");
                Check(!board.EnhancedLighting&&Has("Lighting: Current"),"Visual settings keep current lighting as the default");Capture("02-settings-visuals-current");
                Click("Lighting: Current");
                Check(board.EnhancedLighting&&PlayerPrefs.GetInt(BoardView.LightingKey)==1&&Has("Lighting: Enhanced (experimental)")&&!session.Running&&!session.IsPaused,"Experimental lighting can be enabled and saved before starting a match");Capture("02-settings-visuals-enhanced");
                Click("Fog: 0% (Off)");
                Check(board.FogDensity==0&&PlayerPrefs.GetFloat(BoardView.FogDensityKey)==0&&!session.Running&&!session.IsPaused,"Fog can be disabled and saved before starting a match");Capture("02-settings-visuals-off");
                Click("Fog: 50%");Check(board.FogDensity==.5f&&PlayerPrefs.GetFloat(BoardView.FogDensityKey)==.5f,"Visual settings save intermediate fog density");
                Click("Play space");
                Check(Has("Reposition board")&&Has("Detect surfaces")&&!Has("Table / height 75 cm")&&!Has("Floor / height 0")&&!Has("Hand: Right"),"Play space offers direct placement and surfaces without fixed-height presets");
                Check(console.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Page Up / Page Down")&&t.text.Contains("raise / lower")),"Play space explains height adjustment during placement");Capture("02-settings-play-space");
                Click("Detect surfaces");
                Check(!quest.Active&&!quest.Placing&&!session.Running&&Field<Text>(panel,"status").text==session.Message&&session.Message.Contains("manual plane"),"Detect surfaces shows its desktop result in the Settings footer");Capture("02-settings-surface-feedback");
                typeof(CommandPanel).GetField("menuMessageUntil",Private).SetValue(panel,Time.unscaledTime-1);Refresh();
                Check(Field<Text>(panel,"status").text!=session.Message,"Temporary settings notices expire");
                Click("Detect surfaces");
                Check(Field<Text>(panel,"status").text==session.Message,"Repeating the same operation renews identical result text");
                Click("Back");Check(panel.Page==CommandPanel.MenuPage.Main,"Settings return to main menu");
                Click("Settings");Click("Play space");Click("Detect surfaces");
                Check(Field<Text>(panel,"status").text==session.Message,"Identical operation feedback returns after reopening Settings");Click("Back");
                Click("Settings");Click("Controls");Click("Hotkeys");
                Check(panel.Page==CommandPanel.MenuPage.Hotkeys&&!Field<bool>(panel,"hotkeysTouch"),"Dedicated Hotkeys page defaults to the connected desktop input");
                Check(!Field<RectTransform>(panel,"controlGuide").gameObject.activeInHierarchy,"Hotkeys is a separate reference, not the pause tutorial cards");
                Check(!Has("Quest Touch")&&!Has("Mouse / Keyboard"),"Hotkeys exposes no inactive-device tabs");
                BrowseHotkeys(false);typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);Invoke(panel,"Rebuild");BrowseHotkeys(true);
                Check(Field<Text>(panel,"hotkeysDevice").text.Contains("Right Touch"),"Hotkeys identifies the selected Touch controller");
                Click("Hotkeys: Groups");Click("Next");typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);Invoke(panel,"Rebuild");
                Check(Field<int>(panel,"hotkeysPage")==0,"Switching device resets pagination");
                Check(!session.Running&&!session.IsPaused&&!panel.AcceptsGameInput,"Browsing Hotkeys before a match never starts a match or enables game input");
                ExportDefaults();
                panel.Back();Refresh();Check(panel.Page==CommandPanel.MenuPage.Settings&&!session.IsPaused,"Hotkeys Back returns to Settings before the main menu");
                Click("Back");Click("New Match");
                Check(panel.Page==CommandPanel.MenuPage.MatchSetup&&board.Content.gameObject.activeSelf,"New Match opens setup and map preview");
                Check(Has("Faction: Consortium")&&Has("Opponent: Symbiosis")&&Has("AI: Normal")&&Has("Map: Forest Watch")&&Has("START MATCH")&&Has("Position board")&&Has("Back")&&!Has("Hotkeys")&&!Has("Settings")&&!Has("Reposition board")&&!Has("Detect surfaces")&&!Has("Table / height 75 cm")&&!Has("Floor / height 0"),"New Match includes direct board placement beside Start without adding advanced settings");
                var factionCards=panel.GetComponentsInChildren<SelectionPortrait>();
                Check(factionCards.Length==2&&factionCards.Any(p=>ProductionQueuePreview.MatchesModel(p,session.Catalog.Role(session.Faction,"hq")))
                    &&factionCards.Any(p=>ProductionQueuePreview.MatchesModel(p,session.Catalog.Role(session.EnemyFaction,"hq"))),
                    "New Match identifies both factions with their real headquarters models");
                var startButton=panel.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="START MATCH");
                Check(startButton.rect.width==504&&startButton.rect.height==80&&startButton.GetComponent<Image>().color==ConsoleTheme.Primary,
                    "Start Match is the large teal primary setup action");
                Click("Position board");
                Check(quest.Placing&&!quest.StartPending&&!session.Running&&panel.Page==CommandPanel.MenuPage.MatchSetup,
                    "Direct placement stays in setup and does not request a match");
                panel.Back();Refresh();
                Check(!quest.Placing&&!board.Placed&&panel.Page==CommandPanel.MenuPage.MatchSetup,"Cancel direct placement returns to setup with the board still missing");
                Click("Position board");quest.PlaceRay(new Ray(new Vector3(0,2,0),Vector3.down),0,Vector2.zero,true);Refresh();
                Check(board.Placed&&!quest.Placing&&!quest.StartPending&&!session.Running&&Field<Text>(panel,"harvesting").text.StartsWith("BOARD READY"),
                    "Confirming direct placement prepares the board without starting the match");
                var directPosition=board.transform.position;float directWidth=board.Width;
                Click("Position board");board.transform.position+=Vector3.right;board.Width=2;panel.Back();Refresh();
                Check(board.Placed&&board.transform.position==directPosition&&board.Width==directWidth&&panel.Page==CommandPanel.MenuPage.MatchSetup,
                    "The same direct action repositions a ready board and cancellation preserves its previous placement");
                Capture("05-direct-board-placement");
                // Successful placement detaches the board; restore fixture ownership
                // before the existing anchored-placement cancellation checks.
                board.transform.SetParent(fixture.transform,true);board.Placed=false;
                CheckMapDescription("128 × 128 · 6 zones · 1v1\n24m/6g · Forest");Capture("05-forest-setup");
                Click("Map: Forest Watch");Check(session.MapId=="training"&&session.Map.theme=="neon","Map selection reaches Neon Loop");
                CheckMapDescription("48 × 48 · 4 zones · 1v1\n16m/4g · Futuristic neon");Capture("05-neon-setup");
                Click("Map: Neon Loop");Check(session.MapId=="desert"&&session.Map.width==160&&session.Map.height==160&&session.Map.theme=="desert","Map selection reaches Desser Alert with its desert theme");
                CheckMapDescription("160 × 160 · 8 zones · 1v1\n48m/16g · Desert");Capture("05-desert-setup");
                Click("Map: Desser Alert");Check(session.MapId=="rift","Map selection cycles through all three maps");
                Click("AI: Normal");Check(session.Difficulty=="hard"&&Has("AI: Hard")&&Field<Text>(panel,"harvesting").text.Contains("2:30"),"Setup offers Hard with its earlier attack description");Capture("05-hard");
                Click("AI: Hard");Check(session.Difficulty=="easy"&&Has("AI: Easy")&&Field<Text>(panel,"harvesting").text.Contains("5 min"),"Hard cycles to Easy");
                Click("AI: Easy");Check(session.Difficulty=="normal"&&Has("AI: Normal")&&Field<Text>(panel,"harvesting").text.Contains("3:30"),"Easy cycles to Normal");Click("AI: Normal");
                typeof(QuestBridge).GetField("restoring",Private).SetValue(quest,true);Click("START MATCH");
                Check(quest.StartPending&&!quest.Placing&&!session.Running&&Field<Text>(panel,"status").text=="Restoring your saved board…","A pending start keeps restoration feedback visible without starting placement or a match");
                typeof(CommandPanel).GetField("menuMessageUntil",Private).SetValue(panel,Time.unscaledTime-1);Refresh();
                Check(Field<Text>(panel,"status").text=="Restoring your saved board…","Restoration feedback remains visible while start is pending after temporary notices expire");
                panel.Back();Refresh();typeof(QuestBridge).GetField("restoring",Private).SetValue(quest,false);
                Check(panel.Page==CommandPanel.MenuPage.Main&&!quest.StartPending&&!quest.Placing&&!session.Running,"Back cancels a start waiting for board restoration");
                Click("New Match");Check(session.Difficulty=="hard","Cancelling a pending start preserves the selected difficulty");
                Click("START MATCH");Check(!session.Running&&quest.Placing&&quest.StartPending,"Start opens missing placement before creating a match");
                panel.Back();Refresh();Check(!quest.Placing&&!quest.StartPending&&!session.Running,"Cancelling first placement returns to setup without starting a match");
                board.Placed=true;var placedPosition=new Vector3(.2f,.75f,.1f);board.transform.position=placedPosition;
                var placedRotation=Quaternion.Euler(0,25,0);board.transform.rotation=placedRotation;float placedWidth=board.Width;
                string setupFaction=session.Faction,setupOpponent=session.EnemyFaction,setupMap=session.MapId,setupDifficulty=session.Difficulty;
                Click("Back");Click("Settings");Click("Play space");
                Click("Reposition board");Check(quest.Placing&&!board.Placed,"Placement starts from the shared Play space settings");
                board.transform.position+=Vector3.right;board.Width=2;
                panel.Back();Refresh();
                Check(!quest.Placing&&board.Placed&&!session.IsPaused,"Cancel placement restores readiness and removes only placement pause");
                Check(board.transform.parent==fixture.transform&&board.transform.position==placedPosition&&board.transform.rotation==placedRotation&&board.Width==placedWidth,"Cancel placement restores the board transform and parent");
                Check(panel.Page==CommandPanel.MenuPage.Settings,"Cancel placement returns to the caller's settings page");
                Click("Back");Click("New Match");
                Check(session.Faction==setupFaction&&session.EnemyFaction==setupOpponent&&session.MapId==setupMap&&session.Difficulty==setupDifficulty,"Visiting shared settings preserves the chosen match setup");
                Capture("05-setup");
                session.Pause("tracking",true);Click("START MATCH");
                Check(session.Running&&panel.Page==CommandPanel.MenuPage.Actions&&session.IsPaused&&!panel.AcceptsGameInput,"Starting preserves tracking pause");
                Check(Field<Strategist>(session,"bot").Difficulty==AiDifficulty.Hard,"Starting instantiates the selected Hard AI");
                session.Pause("tracking",false);FinishCountdown();Refresh();
                Check(panel.AcceptsGameInput&&!Has("New Match")&&Has("Pause"),"Match exposes action panel instead of main menu");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);Invoke(panel,"Rebuild");Refresh();
                Check(!Has("Full map")&&Has("Pause"),"Quest HUD removes the obsolete map reset action");Capture("06-actions-touch");
                Click("Pause");Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"Expanded Quest pause button works with the pointer");
                Check(!Field<RectTransform>(panel,"controlGuide").gameObject.activeInHierarchy&&!Has("Help: Selection")&&Has("Guides")&&Has("Settings")&&Has("Resume match")&&Has("Return to main menu"),"Pause presents only resume, guides, settings and leave navigation");
                Check(!board.BaseTargeting&&!Has("Selection: Bodies")&&!Has("Hotkeys")&&!Has("Control guide"),"Pause keeps detailed controls in their shared submenus");
                Check(Field<RectTransform>(panel,"root").sizeDelta.y==690,"Pause uses the shared menu height");
                int pausedTick=session.Match.Tick;var beforeSelection=session.Selection.ToArray();int minerals=session.View.minerals;
                Capture("07-pause-touch");Click("Settings");Click("Controls");
                Click("Selection: Bodies");
                Check(board.BaseTargeting&&PlayerPrefs.GetInt(BoardView.BaseTargetingKey)==1&&Has("Selection: Bases"),"Base targeting is opt-in and saved on this device");
                Check(session.IsPaused&&session.Match.Tick==pausedTick&&session.Selection.SetEquals(beforeSelection),"Changing targeting does not resume or change the selection");
                Capture("07-paused-controls");
                Click("Selection: Bases");Check(!board.BaseTargeting&&PlayerPrefs.GetInt(BoardView.BaseTargetingKey)==0,"Body targeting can be restored from the same button");
                Click("Visuals");Check(board.FogDensity==.5f&&board.EnhancedLighting&&Has("Lighting: Enhanced (experimental)"),"Paused visual settings retain the pre-match density and lighting");
                var lightingMatch=session.Match;
                Click("Lighting: Enhanced (experimental)");
                Check(!board.EnhancedLighting&&PlayerPrefs.GetInt(BoardView.LightingKey)==0&&Has("Lighting: Current")&&session.Match==lightingMatch&&session.IsPaused&&session.Match.Tick==pausedTick&&session.Selection.SetEquals(beforeSelection)&&session.View.minerals==minerals,"Current lighting restores immediately from pause without restarting or changing gameplay");Capture("07-paused-lighting-current");
                Click("Lighting: Current");
                Check(board.EnhancedLighting&&PlayerPrefs.GetInt(BoardView.LightingKey)==1&&session.Match==lightingMatch&&session.IsPaused&&session.Match.Tick==pausedTick&&session.Selection.SetEquals(beforeSelection)&&session.View.minerals==minerals,"Enhanced lighting reapplies immediately in the same paused match");Capture("07-paused-lighting-enhanced");
                Click("Lighting: Enhanced (experimental)");
                Click("Fog: 75%");for(int i=0;i<3;i++)session.Match.Step();
                Check(board.FogDensity==.75f&&PlayerPrefs.GetFloat(BoardView.FogDensityKey)==.75f&&session.IsPaused&&session.Match.Tick==pausedTick&&session.Selection.SetEquals(beforeSelection)&&session.View.minerals==minerals,"Changing fog from pause saves immediately without resuming or changing gameplay");Capture("07-paused-visuals");
                Click("Fog: 100%");
                Click("Back");Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"Shared controls return to the same paused match");
                Click("Guides");Click("Control guide");
                Check(panel.Page==CommandPanel.MenuPage.Guide&&Field<RectTransform>(panel,"controlGuide").gameObject.activeInHierarchy&&!Has("Resume match"),"Guide opens as its own submenu");
                foreach(string topic in new[]{"Selection","Orders","Menus","Groups","Production","Board"})
                {
                    Click("Help: "+topic);for(int i=0;i<3;i++)session.Match.Step();
                    Check(panel.Page==CommandPanel.MenuPage.Guide&&session.IsPaused&&session.Match.Tick==pausedTick&&!panel.AcceptsGameInput,"Browsing "+topic+" keeps simulation paused and gameplay input blocked");
                    Capture("07-guide-touch-"+topic.ToLowerInvariant());
                    if(topic=="Selection")
                    {
                        Click("Page 1 / 2 · Next");
                        Check(Field<Text[]>(panel,"controlGuideCommands").Take(2).Select(t=>t.text).SequenceEqual(new[]{"Select idle workers","Select all army"})&&session.IsPaused,
                            "The selection guide exposes the quick-selection controls on its second page without resuming");
                        Capture("07-guide-touch-selection-quick");
                        Click("Page 2 / 2 · Next");
                        Check(Field<int>(panel,"controlGuidePage")==0,"Guide pagination returns to the first selection page");
                    }
                }
                Check(session.Selection.SetEquals(beforeSelection)&&session.View.minerals==minerals,"Guide cards never change selection or resources");
                Click("Back");Check(panel.Page==CommandPanel.MenuPage.Guides&&session.IsPaused&&!Has("Help: Selection"),"Guide Back returns to its hub and removes detail targets");
                foreach(var entry in new[]{("Units",CommandPanel.MenuPage.UnitsGuide),("Structures",CommandPanel.MenuPage.StructuresGuide)})
                {
                    Click(entry.Item1);for(int i=0;i<3;i++)session.Match.Step();
                    Check(panel.Page==entry.Item2&&session.IsPaused&&session.Match.Tick==pausedTick&&!panel.AcceptsGameInput&&session.Selection.SetEquals(beforeSelection)&&session.View.minerals==minerals,"Reading "+entry.Item1+" preserves the paused match and selection");
                    Click("Back");Check(panel.Page==CommandPanel.MenuPage.Guides&&session.IsPaused,"Catalog Back returns to Guides without resuming");
                }
                Click("Control guide");panel.ToggleMenu();Refresh();
                Check(panel.Page==CommandPanel.MenuPage.Guides&&session.IsPaused,"The menu shortcut also returns from Guide to its hub without resuming");
                Click("Back");Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"Guides Back returns to the original pause menu");
                Click("Resume match");Check(!Field<RectTransform>(panel,"controlGuide").gameObject.activeInHierarchy,"Resuming hides the guide and removes its targets");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);Invoke(panel,"Rebuild");Refresh();
                var desktopPause=panel.GetComponentsInChildren<RectTransform>().FirstOrDefault(t=>t.gameObject.activeInHierarchy&&t.name=="Pause");
                Check(!Has("Full map")&&desktopPause!=null&&desktopPause.rect.height>=84,"Desktop HUD keeps a large Pause control without the removed Full map action");
                var hudRoot=Field<RectTransform>(panel,"root");var resources=Field<RectTransform>(panel,"resourceStrip");
                var footer=Field<RectTransform>(panel,"matchStrip");var clock=Field<Text>(panel,"matchClock");
                var groups=Field<RectTransform>(panel,"groupStrip");var selectedBox=Field<RectTransform>(panel,"selectedCard");
                Check(footer.gameObject.activeInHierarchy&&desktopPause.parent==footer&&clock.transform.parent==footer
                    &&-footer.anchoredPosition.y>=-groups.anchoredPosition.y+groups.rect.height
                    &&hudRoot.InverseTransformPoint(desktopPause.TransformPoint(desktopPause.rect.center)).x>hudRoot.rect.center.x&&!Has("Reset menu"),
                    "HUD places Pause and elapsed time in the bottom row after the game sections");
                Check(!Field<Text>(panel,"consoleBrand").gameObject.activeInHierarchy&&!Field<Text>(panel,"consoleBrandSmall").gameObject.activeInHierarchy
                    &&-resources.anchoredPosition.y>=40&&-selectedBox.anchoredPosition.y+resources.anchoredPosition.y-resources.rect.height>=40,
                    "Resources lead the HUD without a logo and keep breathing room above and below");
                Check(Field<Text>(panel,"matchMap").text==session.Map.title&&Field<Text>(panel,"matchInfo").gameObject.activeInHierarchy,
                    "Bottom row identifies the current map and match");
                var match=session.Match;var worker=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                session.Select(worker.id,false);session.Group(0,true);session.ChooseBuild(session.Catalog.Role(session.Faction,"supply").id);
                Invoke(board,"LateUpdate");Refresh();Capture("06-actions");
                Click("Pause");int tick=match.Tick;for(int i=0;i<30;i++)match.Step();
                Check(session.IsPaused&&match.Tick==tick&&!panel.AcceptsGameInput,"Pause stops simulation and blocks game input");Capture("07-pause");
                Click("Settings");Click("Back");Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"In-match settings return to pause");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                Click("Settings");Click("Controls");Click("Hotkeys");Check(Field<bool>(panel,"hotkeysTouch"),"Opening through Quest pause settings defaults to Touch even after desktop preview");
                int hotkeysTick=match.Tick;var hotkeysSelection=session.Selection.ToArray();int hotkeysMinerals=session.View.minerals;string buildMode=session.BuildDefinition;
                Click("Hotkeys: Groups");Click("Next");Click("Previous");
                for(int i=0;i<30;i++)match.Step();
                var row=Field<RectTransform[]>(panel,"hotkeyRows")[0];var rowCenter=row.TransformPoint(row.rect.center);var rowRay=new Ray(rowCenter-row.forward,row.forward);
                panel.Point(rowRay,true,false);panel.Point(rowRay,false,true);Refresh();
                Check(match.Tick==hotkeysTick&&session.IsPaused&&!panel.AcceptsGameInput&&session.Selection.SetEquals(hotkeysSelection)&&session.View.minerals==hotkeysMinerals&&session.BuildDefinition==buildMode&&session.GroupSize(0)==1,"Hotkey browsing and clicking read-only rows preserve pause, selection, resources, build mode and groups");
                Capture("07-paused-hotkeys");panel.Back();Refresh();
                Check(panel.Page==CommandPanel.MenuPage.Settings&&session.IsPaused&&!Field<RectTransform>(panel,"hotkeys").gameObject.activeInHierarchy,"B / Back returns Hotkeys to Settings and hides all reference targets");
                Click("Back");Check(panel.Page==CommandPanel.MenuPage.Pause&&session.IsPaused,"Settings retains Pause as its caller after visiting Hotkeys");
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);
                Click("Return to main menu");Check(panel.Page==CommandPanel.MenuPage.LeaveConfirmation&&session.Running,"Leaving an active match asks before losing progress");
                Check(!Has("Back")&&panel.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Keep playing").GetComponent<Image>().color==ConsoleTheme.Success
                    &&panel.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Leave match").GetComponent<Image>().color==ConsoleTheme.Danger,
                    "Leave confirmation has only green Keep playing and red Leave match choices");
                Capture("08-leave-confirmation");
                Click("Keep playing");for(int i=0;i<30;i++)match.Step();
                Check(panel.Page==CommandPanel.MenuPage.Pause&&session.Match==match&&session.IsPaused&&match.Tick==tick&&!panel.AcceptsGameInput&&session.Selection.Contains(worker.id)&&session.GroupSize(0)==1,"Cancel leaving returns to Pause without resuming or changing the match, selection or groups");
                Click("Resume match");
                session.Pause("headset",true);panel.ToggleMenu();Refresh();Click("Resume match");
                Check(session.IsPaused&&!panel.AcceptsGameInput,"Resume does not clear headset pause");
                panel.ToggleMenu();Refresh();Click("Return to main menu");Click("Leave match");
                Check(panel.Page==CommandPanel.MenuPage.Main&&!session.Running&&session.Match==null&&session.View==null,"Leave returns to main and releases simulation and view");
                Check(session.Selection.Count==0&&session.SelectedResource==0&&session.GroupSize(0)==0&&session.BuildDefinition==null&&!session.AttackMoveMode,"Leave clears all match input state");
                Check(session.IsPaused&&match.Paused,"Leave preserves external pause and freezes old match");
                Check(!board.Content.gameObject.activeSelf&&Field<System.Collections.IDictionary>(board,"entities").Count==0&&Field<System.Collections.IDictionary>(board,"resources").Count==0,"Leave clears old world visuals and hides board");
                Check(board.Placed&&board.transform.position==placedPosition&&board.transform.rotation==placedRotation&&board.transform.parent==fixture.transform,"Leave retains physical board placement");Capture("09-returned-main");
                session.Pause("headset",false);Click("New Match");Click("Faction: Consortium");Click("Map: Forest Watch");
                Check(session.Faction=="biological"&&session.MapId=="training","A new setup can change faction and map");Click("START MATCH");
                Check(session.Running&&session.Match!=match&&session.Match.Tick==0&&session.Match.Winner==-1&&session.Selection.Count==0&&!session.IsPaused,"Next match starts clean");
                Check(session.View.entities.Any(e=>e.owner==0&&e.definition=="biological.hq"),"Next match uses the new faction");
                Check(Field<Strategist>(session,"bot").Difficulty==AiDifficulty.Hard,"Next match retains selected difficulty");
                session.Send(CommandKind.Surrender);Refresh();
                Check(panel.Page==CommandPanel.MenuPage.Result&&Has("Rematch")&&Has("Main menu")&&!Has("Resume match"),"Finished match offers rematch or main menu only");Capture("10-result");
                var finished=session.Match;Click("Rematch");
                Check(session.Match!=finished&&session.Match.Winner==-1&&panel.Page==CommandPanel.MenuPage.Actions&&!session.IsPaused,"Rematch resets result and menu state");
                Check(Field<Strategist>(session,"bot").Difficulty==AiDifficulty.Hard,"Rematch retains selected difficulty");
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
                    var miss=new Ray(center+button.right*100-button.forward,button.forward);
                    // Consecutive clicks can leave the same button hovered; establish its resting colors first.
                    panel.Point(miss,false,false);
                    var resting=button.GetComponent<Image>().color;
                    var ink=button.GetComponentsInChildren<Graphic>(true).Where(g=>g is Text||g is ConsoleGlyph).ToDictionary(g=>g,g=>g.color);
                    var portraits=button.GetComponentsInChildren<SelectionPortrait>(true).ToDictionary(p=>p,p=>p.color);
                    panel.Point(ray,false,false);
                    Check(button.GetComponent<Image>().color==ConsoleTheme.Accent&&ink.Keys.All(g=>g.color==ConsoleTheme.HoverInk)
                        &&portraits.All(p=>p.Key.color==p.Value),"Accent hover has dark readable ink and preserves model colors: "+name);
                    panel.Point(miss,false,false);
                    Check(button.GetComponent<Image>().color==resting&&ink.All(g=>g.Key.color==g.Value),"Leaving hover restores button and label colors: "+name);
                    if(!panel.Point(ray,true,false)||!panel.Point(ray,false,true))throw new Exception("Menu ray did not hit: "+name);
                    FinishCountdown();
                    Refresh();
                }
                // Edit-mode menus have no frame loop; countdown behavior is covered
                // separately before these navigation and layout regressions run.
                void FinishCountdown()
                {
                    if(session.IsCountingDown&&!session.HasSystemPause&&!quest.Placing)
                        for(int i=0;i<12;i++)Invoke(session,"AdvanceCountdown",.25f);
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
                void CheckMapDescription(string expected)
                {
                    Canvas.ForceUpdateCanvases();
                    var label=panel.GetComponentsInChildren<Text>().SingleOrDefault(t=>t.text==expected);
                    Check(label!=null,"Map card shows consistent dimensions, resource zones, deposits and match format: "+session.Map.title);
                    Check(label.preferredHeight<=label.rectTransform.rect.height+.5f,"Both map detail lines fit the card: "+session.Map.title);
                }
                void Capture(string name)
                {
                    Refresh();
                    var window=Field<RectTransform>(panel,"root");var grip=Field<RectTransform>(panel,"moveHandle");
                    bool main=panel.Page==CommandPanel.MenuPage.Main;
                    Check(Field<Text>(panel,"consoleBrand").gameObject.activeInHierarchy==main
                        &&Field<Text>(panel,"consoleBrandSmall").gameObject.activeInHierarchy==main,
                        "Only the initial menu shows the game wordmark: "+panel.Page);
                    Check(!Field<RectTransform>(panel,"resetHandle").gameObject.activeInHierarchy
                        &&Field<RectTransform>(panel,"moveAffordance").gameObject.activeInHierarchy
                        &&Mathf.Abs(grip.anchoredPosition.x+grip.rect.width*.5f-window.rect.width*.5f)<.01f
                        &&-grip.anchoredPosition.y>window.rect.height,
                        "Every page has the same centered lower grip, without a reset header: "+panel.Page);
                    if(main)Check(Field<Text>(panel,"consoleBrand").fontSize>=70&&!Field<Text>(panel,"headline").gameObject.activeInHierarchy
                        &&!Field<Text>(panel,"selection").gameObject.activeInHierarchy,
                        "Initial menu has a large wordmark without duplicate title or introductory paragraph");
                    var guide=Field<RectTransform>(panel,"controlGuide");
                    if(panel.Page==CommandPanel.MenuPage.Settings&&(Has("Lighting: Current")||Has("Lighting: Enhanced (experimental)")))
                    {
                        Canvas.ForceUpdateCanvases();
                        foreach(var text in Field<RectTransform>(panel,"actions").GetComponentsInChildren<Text>())
                            if(text.preferredHeight>text.rectTransform.rect.height+.5f)throw new Exception("Clipped visual settings text: "+text.text);
                    }
                    if(guide.gameObject.activeInHierarchy)
                    {
                        Canvas.ForceUpdateCanvases();
                        foreach(var text in guide.GetComponentsInChildren<Text>())
                            if(text.preferredHeight>text.rectTransform.rect.height+.5f)throw new Exception("Clipped guide text: "+text.text);
                        var actions=Field<RectTransform>(panel,"actions");
                        float guideTop=-guide.anchoredPosition.y,guideBottom=guideTop+guide.sizeDelta.y;
                        float actionsTop=-actions.anchoredPosition.y,actionsBottom=actionsTop+actions.sizeDelta.y;
                        if(!(actionsBottom<=guideTop||guideBottom<=actionsTop))throw new Exception("Control guide overlaps navigation buttons");
                        if(guideBottom>-Field<RectTransform>(panel,"statusBanner").anchoredPosition.y)throw new Exception("Control guide overlaps footer");
                    }
                    var reference=Field<RectTransform>(panel,"hotkeys");
                    if(reference.gameObject.activeInHierarchy)
                    {
                        Canvas.ForceUpdateCanvases();
                        foreach(var text in reference.GetComponentsInChildren<Text>())
                            if(text.preferredHeight>text.rectTransform.rect.height+.5f)throw new Exception("Clipped hotkey text: "+text.text);
                        var actions=Field<RectTransform>(panel,"actions");
                        if(-reference.anchoredPosition.y+reference.sizeDelta.y>-actions.anchoredPosition.y)throw new Exception("Hotkeys overlaps navigation");
                        if(-actions.anchoredPosition.y+actions.sizeDelta.y>-Field<RectTransform>(panel,"statusBanner").anchoredPosition.y)throw new Exception("Hotkey navigation overlaps footer");
                    }
                    HarvestingPreview.SavePanel(panel,"TestResults/Menus/"+name+".png",preview);captures++;
                }
            }
            finally
            {
                if(hadBasePreference)PlayerPrefs.SetInt(BoardView.BaseTargetingKey,basePreference);else PlayerPrefs.DeleteKey(BoardView.BaseTargetingKey);
                if(hadFogPreference)PlayerPrefs.SetFloat(BoardView.FogDensityKey,fogPreference);else PlayerPrefs.DeleteKey(BoardView.FogDensityKey);
                if(hadLightingPreference)PlayerPrefs.SetInt(BoardView.LightingKey,lightingPreference);else PlayerPrefs.DeleteKey(BoardView.LightingKey);
                if(hadLanguagePreference)PlayerPrefs.SetString(Localization.PreferenceKey,languagePreference);else PlayerPrefs.DeleteKey(Localization.PreferenceKey);
                Localization.ReloadPreference();
                PlayerPrefs.Save();
                Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();
            }
        }
    }
}
