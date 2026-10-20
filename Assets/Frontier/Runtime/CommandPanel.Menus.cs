using System;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private enum SettingsTab { Controls, Audio, PlaySpace }
        private SettingsTab settingsTab;
        private string MenuOrigin=>Session.Running?"PAUSE":"MAIN MENU";
        private static readonly Color MenuMuted=new Color(.69f,.8f,.85f);

        // Shared spacing, typography and action hierarchy across all out-of-match pages.
        // Titles remain separate from descriptions so pointing targets are unambiguous.
        private void MenuCard(string title,string description,Vector2 position,Vector2 size,Action click,bool primary=false)
        {
            var rect=Section(actions,title,position,size,primary?new Color(.075f,.27f,.3f):button);
            Section(rect,"Accent",Vector2.zero,new Vector2(4,size.y),primary?accent:new Color(.17f,.38f,.44f));
            bool compact=size.y<80;
            Label(rect,title,new Vector2(22,compact?-8:-15),new Vector2(size.x-72,32),primary?26:23,Color.white);
            Label(rect,description,new Vector2(22,compact?-40:-54),new Vector2(size.x-58,compact?22:size.y-62),compact?16:18,MenuMuted,TextAnchor.UpperLeft);
            Label(rect,"›",new Vector2(size.x-42,-12),new Vector2(26,38),26,primary?accent:MenuMuted,TextAnchor.MiddleCenter);
            targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=click});
        }

        private void MenuFooter(string title,Action click,float x=0,float width=330,bool primary=false)
        {
            var rect=Section(actions,title,new Vector2(x,-338),new Vector2(width,58),primary?new Color(.075f,.27f,.3f):button);
            Label(rect,title,new Vector2(18,0),new Vector2(width-36,58),22,primary?accent:Color.white,TextAnchor.MiddleCenter);
            targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=click});
        }

        private bool BuildMenuPage()
        {
            switch(Page)
            {
                case MenuPage.Main:
                    MenuCard("New Match","Choose your faction and battlefield. Play a local skirmish against the AI.",Vector2.zero,new Vector2(1040,124),()=>Show(MenuPage.MatchSetup),true);
                    MenuCard("Guides","Controls, units and structures. Everything you need to plan your next move.",new Vector2(0,-142),new Vector2(512,154),()=>OpenSubmenu(MenuPage.Guides));
                    MenuCard("Settings","Controls, audio and play space. Your preferences, together in one place.",new Vector2(528,-142),new Vector2(512,154),()=>OpenSubmenu(MenuPage.Settings));
                    return true;
                case MenuPage.MatchSetup:
                    MenuCard("Faction: "+ShortFaction(Session.Faction),"Your army · Select to switch faction.",Vector2.zero,new Vector2(512,108),()=>{Session.Faction=Other(Session.Faction);Rebuild();});
                    MenuCard("Opponent: "+ShortFaction(Session.EnemyFaction),"Enemy army · Select to switch faction.",new Vector2(528,0),new Vector2(512,108),()=>{Session.EnemyFaction=Other(Session.EnemyFaction);Rebuild();});
                    MenuCard("AI: "+DifficultyLabel,Session.Difficulty=="easy"?"More time to build and learn the controls.":Session.Difficulty=="hard"?"Fast economy and sustained pressure.":"Steady attacks, reinforcements and abilities.",new Vector2(0,-124),new Vector2(512,108),()=>{Session.Difficulty=Session.Difficulty=="easy"?"normal":Session.Difficulty=="normal"?"hard":"easy";Rebuild();});
                    MenuCard("Map: "+Session.Map.title,Session.MapId=="desert"?"160 × 160 · Desert plateaus · 8 resource zones.":"Battlefield layout · Select to switch map.",new Vector2(528,-124),new Vector2(512,108),()=>{Session.CycleMap();Board.Rebuild();Rebuild();});
                    MenuFooter("Back",Back);
                    MenuFooter("START MATCH",Quest.RequestStartMatch,352,688,true);
                    return true;
                case MenuPage.Pause:
                    MenuCard("Resume match","Return to the battlefield.",Vector2.zero,new Vector2(1040,100),()=>Show(MenuPage.Actions),true);
                    MenuCard("Guides","Controls and the unit / structure reference.",new Vector2(0,-118),new Vector2(512,116),()=>OpenSubmenu(MenuPage.Guides));
                    MenuCard("Settings","Controls, audio and play space.",new Vector2(528,-118),new Vector2(512,116),()=>OpenSubmenu(MenuPage.Settings));
                    MenuFooter("Return to main menu",()=>Show(MenuPage.LeaveConfirmation),0,512);
                    return true;
                case MenuPage.Guides:
                    MenuCard("Control guide","Selection, orders, groups, production and board gestures.",Vector2.zero,new Vector2(336,218),()=>OpenSubmenu(MenuPage.Guide));
                    MenuCard("Units","Compare every unit: combat stats, cost, training and abilities.",new Vector2(352,0),new Vector2(336,218),()=>OpenSubmenu(MenuPage.UnitsGuide));
                    MenuCard("Structures","Plan your base: construction, requirements and production.",new Vector2(704,0),new Vector2(336,218),()=>OpenSubmenu(MenuPage.StructuresGuide));
                    MenuFooter("Back",Back);return true;
                case MenuPage.Settings:
                    BuildSettingsPage();return true;
                case MenuPage.LeaveConfirmation:
                    MenuCard("Keep playing","Resume your current match.",Vector2.zero,new Vector2(512,140),()=>Show(MenuPage.Actions),true);
                    MenuCard("Leave match","End this match and return to the main menu.",new Vector2(528,0),new Vector2(512,140),()=>Session.LeaveMatch());
                    MenuFooter("Back",Back);return true;
                case MenuPage.Result:
                    MenuCard("Rematch","Start again with the same factions, difficulty and battlefield.",Vector2.zero,new Vector2(512,154),Quest.RequestStartMatch,true);
                    MenuCard("Main menu","Choose another match, review the guides or adjust settings.",new Vector2(528,0),new Vector2(512,154),()=>Session.LeaveMatch());
                    return true;
                default:return false;
            }
        }

        private void BuildSettingsPage()
        {
            var names=new[]{"Controls","Audio","Play space"};
            for(int i=0;i<names.Length;i++)
            {
                var tab=(SettingsTab)i;bool active=settingsTab==tab;
                var rect=Section(actions,names[i],new Vector2(i*352,0),new Vector2(336,48),active?new Color(.075f,.27f,.3f):button);
                Label(rect,names[i],Vector2.zero,new Vector2(336,48),21,active?accent:Color.white,TextAnchor.MiddleCenter);
                if(active)Section(rect,"Selected",new Vector2(0,-45),new Vector2(336,3),accent);
                targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=()=>{settingsTab=tab;Rebuild();UpdateMenuText();}});
            }
            if(settingsTab==SettingsTab.Controls)
            {
                MenuCard("Hand: "+Bindings.HandName,"Switch the active controller and its button hints.",new Vector2(0,-70),new Vector2(512,108),ToggleControllerHand);
                MenuCard("Hotkeys","Customize gameplay buttons and keyboard shortcuts.",new Vector2(528,-70),new Vector2(512,108),()=>OpenSubmenu(MenuPage.Hotkeys));
                MenuCard("Selection: "+(Board.BaseTargeting?"Bases":"Bodies"),Board.BaseTargeting?"Aim at unit bases, including behind buildings and minerals.":"Aim at visible models. Switch to Bases to reach obscured units.",new Vector2(0,-194),new Vector2(1040,108),()=>{Board.SetBaseTargeting(!Board.BaseTargeting);Rebuild();});
            }
            else if(settingsTab==SettingsTab.Audio)
            {
                MenuCard("Music: "+Mathf.RoundToInt(AudioFeedback.MusicVolume*100)+"%","Background soundtrack · Select to change volume.",new Vector2(0,-70),new Vector2(512,130),()=>{AudioFeedback.CycleMusicVolume();Rebuild();});
                MenuCard("Effects: "+Mathf.RoundToInt(AudioFeedback.EffectsVolume*100)+"%","Orders, combat and interface sounds · Select to change volume.",new Vector2(528,-70),new Vector2(512,130),()=>{AudioFeedback.CycleEffectsVolume();Rebuild();});
            }
            else
            {
                MenuCard("Reposition board","Place, rotate and resize your battlefield.",new Vector2(0,-70),new Vector2(512,108),()=>PlaceRequested?.Invoke());
                MenuCard("Detect surfaces","Refresh detected room surfaces for placement.",new Vector2(528,-70),new Vector2(512,108),()=>Quest.LoadRoom());
                MenuCard("Table / height 75 cm","Place at a comfortable tabletop height.",new Vector2(0,-194),new Vector2(512,108),()=>{Quest.SetManualHeight(.75f);PlaceRequested?.Invoke();});
                MenuCard("Floor / height 0","Place the battlefield at floor height.",new Vector2(528,-194),new Vector2(512,108),()=>{Quest.SetManualHeight(0);PlaceRequested?.Invoke();});
            }
            MenuFooter("Back",Back);
        }
    }
}
