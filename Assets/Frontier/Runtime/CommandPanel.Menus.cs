using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private enum SettingsTab { Controls, Audio, Visuals, PlaySpace, Language }
        private SettingsTab settingsTab;
        private string MenuOrigin=>Session.Running?(Session.IsOnline?"MATCH MENU":"PAUSE"):"MAIN MENU";
        private static readonly Color MenuMuted=new Color(.69f,.8f,.85f);
        private string MapDescription
        {
            get
            {
                var map=Session.Map;
                int zones=map.resources.Select(r=>r.id.Split('.')[0]).Distinct().Count();
                int minerals=map.resources.Count(r=>r.kind=="mineral"),gas=map.resources.Count(r=>r.kind=="catalyst");
                string theme=map.theme=="neon"?"Futuristic neon":map.theme=="forest"?"Forest":map.theme=="desert"?"Desert":"Temperate";
                return $"{map.width} × {map.height} · {zones} zones · 1v1\n{minerals}m/{gas}g · {theme}";
            }
        }

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
                    MenuCard("New Match","Choose your faction and battlefield. Play offline against the AI.",Vector2.zero,new Vector2(512,124),()=>Show(MenuPage.MatchSetup),true);
                    MenuCard("Multiplayer LAN","Join a dedicated server on your network. Two human players.",new Vector2(528,0),new Vector2(512,124),()=>Show(MenuPage.OnlineSetup));
                    MenuCard("Guides","Controls, units and structures. Everything you need to plan your next move.",new Vector2(0,-142),new Vector2(512,116),()=>OpenSubmenu(MenuPage.Guides));
                    MenuCard("Settings","Controls, audio, visuals and play space. Your preferences in one place.",new Vector2(528,-142),new Vector2(512,116),()=>OpenSubmenu(MenuPage.Settings));
                    MenuCard("Match history","Watch replays and review saved reports, graphs and build orders.",new Vector2(0,-274),new Vector2(1040,108),OpenHistory);
                    return true;
                case MenuPage.History:BuildHistory();return true;
                case MenuPage.Replay:BuildReplayPanel();return true;
                case MenuPage.OnlineSetup:BuildOnlineSetup();return true;
                case MenuPage.OnlineEndpoint:BuildOnlineEndpoint();return true;
                case MenuPage.MatchSetup:
                    MenuCard("Faction: "+ShortFaction(Session.Faction),"Your army · Select to switch faction.",Vector2.zero,new Vector2(512,108),()=>{Session.Faction=Other(Session.Faction);Rebuild();});
                    MenuCard("Opponent: "+ShortFaction(Session.EnemyFaction),"Enemy army · Select to switch faction.",new Vector2(528,0),new Vector2(512,108),()=>{Session.EnemyFaction=Other(Session.EnemyFaction);Rebuild();});
                    MenuCard("AI: "+DifficultyLabel,Session.Difficulty=="easy"?"More time to build and learn the controls.":Session.Difficulty=="hard"?"Fast economy and sustained pressure.":"Steady attacks, reinforcements and abilities.",new Vector2(0,-124),new Vector2(512,108),()=>{Session.Difficulty=Session.Difficulty=="easy"?"normal":Session.Difficulty=="normal"?"hard":"easy";Rebuild();});
                    MenuCard("Map: "+Session.Map.title,MapDescription,new Vector2(528,-124),new Vector2(512,108),()=>{Session.CycleMap();Board.Rebuild();Rebuild();});
                    MenuFooter("Back",Back,0,336);
                    MenuFooter("Position board",()=>{Quest.CancelStartRequest();PlaceRequested?.Invoke();},352,336);
                    MenuFooter("START MATCH",Quest.RequestStartMatch,704,336,true);
                    return true;
                case MenuPage.Pause:
                    MenuCard("Resume match","Return to the battlefield.",Vector2.zero,new Vector2(1040,100),()=>Show(MenuPage.Actions),true);
                    MenuCard("Guides","Controls and the unit / structure reference.",new Vector2(0,-118),new Vector2(512,116),()=>OpenSubmenu(MenuPage.Guides));
                    MenuCard("Settings","Controls, audio, visuals and play space.",new Vector2(528,-118),new Vector2(512,116),()=>OpenSubmenu(MenuPage.Settings));
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
                    MenuCard("Leave match",Session.IsOnline?"Concede to your opponent and return to the main menu.":"End this match and return to the main menu.",new Vector2(528,0),new Vector2(512,140),()=>Session.LeaveMatch());
                    MenuFooter("Back",Back);return true;
                case MenuPage.Result:case MenuPage.HistoryReport:
                    BuildMatchReport();
                    return true;
                default:return false;
            }
        }

        private void BuildSettingsPage()
        {
            var names=new[]{"Controls","Audio","Visuals","Play space","Language"};
            for(int i=0;i<names.Length;i++)
            {
                var tab=(SettingsTab)i;bool active=settingsTab==tab;
                var rect=Section(actions,names[i],new Vector2(i*211,0),new Vector2(196,48),active?new Color(.075f,.27f,.3f):button);
                Label(rect,names[i],Vector2.zero,new Vector2(196,48),20,active?accent:Color.white,TextAnchor.MiddleCenter);
                if(active)Section(rect,"Selected",new Vector2(0,-45),new Vector2(196,3),accent);
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
            else if(settingsTab==SettingsTab.Visuals)
            {
                int density=Mathf.RoundToInt(Board.FogDensity*100);
                Label(actions,"Fog density: "+density+"%"+(density==0?" (Off)":""),new Vector2(22,-82),new Vector2(996,36),26,Color.white);
                Label(actions,"Visual mist over unseen terrain. Unseen enemies stay hidden.",new Vector2(22,-130),new Vector2(996,50),18,MenuMuted);
                for(int i=0;i<=4;i++)
                {
                    int percent=i*25;bool active=density==percent;
                    string title=percent==0?"0% (Off)":percent+"%";
                    var rect=Section(actions,"Fog: "+title,new Vector2(i*211,-194),new Vector2(196,72),active?new Color(.075f,.27f,.3f):button);
                    Label(rect,title,Vector2.zero,new Vector2(196,72),23,active?accent:Color.white,TextAnchor.MiddleCenter);
                    if(active)Section(rect,"Selected",new Vector2(0,-68),new Vector2(196,4),accent);
                    targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=()=>{Board.SetFogDensity(percent/100f);Rebuild();}});
                }
                Label(actions,"0% removes the mist. 100% is the original density.",new Vector2(22,-284),new Vector2(996,30),18,MenuMuted);
            }
            else if(settingsTab==SettingsTab.Language)
            {
                Label(actions,"Language",new Vector2(22,-76),new Vector2(996,36),26,Color.white);
                Label(actions,"Use your system language or choose one for this device. Changes apply immediately.",new Vector2(22,-122),new Vector2(996,48),18,MenuMuted);
                // One selector enumerates the installed catalogs, with no fixed language limit.
                string choice=Localization.Preference==Localization.SystemPreference?"System · "+Localization.CurrentName:Localization.CurrentName;
                MenuCard("Language: "+choice,"Select to change · System follows the device language.",new Vector2(0,-188),new Vector2(1040,108),()=>
                {
                    var options=new System.Collections.Generic.List<string>{Localization.SystemPreference};
                    foreach(var language in Localization.Languages)options.Add(language.code);
                    Localization.SetPreference(options[(options.IndexOf(Localization.Preference)+1)%options.Count]);
                    Rebuild();UpdateMenuText();
                });
            }
            else
            {
                MenuCard("Reposition board","Place your battlefield and adjust its height as you aim.",new Vector2(0,-70),new Vector2(512,108),()=>PlaceRequested?.Invoke());
                MenuCard("Detect surfaces","Refresh detected room surfaces for placement.",new Vector2(528,-70),new Vector2(512,108),()=>Quest.LoadRoom());
                Label(actions,"Adjust while placing",new Vector2(22,-204),new Vector2(996,34),24,Color.white);
                Label(actions,Quest.Active
                    ?"Grip: shrink · Trigger: enlarge · "+Bindings.MenuConfirmName+": confirm · "+Bindings.MenuBackName+": cancel\nStick ↑↓: height · ←→: rotate · Stick click: show / hide hints"
                    :"Page Up / Page Down: raise / lower · Left-click: confirm · Escape: cancel\nTap for a small height change, or hold to keep adjusting.",
                    new Vector2(22,-248),new Vector2(996,64),18,MenuMuted);
            }
            MenuFooter("Back",Back);
        }
    }
}
