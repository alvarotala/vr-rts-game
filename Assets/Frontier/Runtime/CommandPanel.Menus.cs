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
        private static readonly Color MenuMuted=MenuPalette.Muted;
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
        private RectTransform MenuCard(string title,string description,Vector2 position,Vector2 size,Action click,bool primary=false)
        {
            var rect=Section(actions,title,position,size,primary?MenuPalette.Primary:button);
            if(primary)rect.GetComponent<MenuSurface>().BorderColor=accent;
            bool compact=size.y<80,tall=size.y>=200;
            var symbol=CardSymbol(title);
            float titleX=symbol.HasValue&&!tall?66:24;
            if(symbol.HasValue)
            {
                if(tall)
                {
                    var badge=Section(rect,"Category emblem",new Vector2(24,-20),new Vector2(58,58),MenuPalette.Surface);
                    badge.GetComponent<MenuSurface>().Style=MenuSurface.SurfaceStyle.Inset;badge.GetComponent<MenuSurface>().Radius=18;
                    Icon(badge,symbol.Value,new Vector2(11,-11),36,accent);
                }
                else Icon(rect,symbol.Value,new Vector2(24,compact?-10:-19),26,accent);
            }
            Label(rect,title,new Vector2(titleX,tall?-91:compact?-8:-15),new Vector2(size.x-titleX-48,32),primary?26:23,MenuPalette.Ink).fontStyle=FontStyle.Bold;
            Label(rect,description,new Vector2(24,tall?-132:compact?-40:-54),new Vector2(size.x-48,compact?22:tall?size.y-142:size.y-62),compact?16:17,MenuMuted,TextAnchor.UpperLeft);
            Icon(rect,MenuIcon.Symbol.Chevron,new Vector2(size.x-40,tall?-35:compact?-12:-21),20,primary?accent:MenuMuted);
            targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=click});
            return rect;
        }

        private static MenuIcon.Symbol? CardSymbol(string title)
        {
            if(title.StartsWith("Faction:")||title.StartsWith("Opponent:")||title=="Units")return MenuIcon.Symbol.Army;
            if(title.StartsWith("Map:")||title=="Position board"||title=="Reposition board"||title=="Detect surfaces")return MenuIcon.Symbol.Board;
            if(title.StartsWith("AI:"))return MenuIcon.Symbol.Orders;
            if(title.StartsWith("Hand:")||title=="Hotkeys"||title=="Control guide"||title.StartsWith("Selection:"))return MenuIcon.Symbol.Controls;
            if(title.StartsWith("Music:")||title.StartsWith("Effects:"))return MenuIcon.Symbol.Audio;
            if(title.StartsWith("Lighting:"))return MenuIcon.Symbol.Visuals;
            if(title.StartsWith("Language:"))return MenuIcon.Symbol.Language;
            if(title=="Structures")return MenuIcon.Symbol.Build;
            if(title=="Keep playing")return MenuIcon.Symbol.Play;
            if(title=="Leave match")return MenuIcon.Symbol.Home;
            return null;
        }

        private void MenuFooter(string title,Action click,float x=0,float width=330,bool primary=false)
        {
            var rect=Section(actions,title,new Vector2(x,-326),new Vector2(width,58),primary?MenuPalette.Primary:button);
            Label(rect,title,new Vector2(42,0),new Vector2(width-64,58),22,MenuPalette.Ink,TextAnchor.MiddleCenter).fontStyle=primary?FontStyle.Bold:FontStyle.Normal;
            if(title=="Back")Icon(rect,MenuIcon.Symbol.Back,new Vector2(18,-18),22,accent);
            else if(title=="Return to main menu")Icon(rect,MenuIcon.Symbol.Home,new Vector2(18,-17),24,MenuPalette.Ink);
            else if(title=="START MATCH")Icon(rect,MenuIcon.Symbol.Play,new Vector2(18,-17),24,MenuPalette.Ink);
            else if(title=="Position board")Icon(rect,MenuIcon.Symbol.Board,new Vector2(18,-17),24,MenuPalette.Ink);
            targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=click});
        }

        private bool BuildMenuPage()
        {
            switch(Page)
            {
                case MenuPage.Main:
                    NavigationCard("New Match",MenuIcon.Symbol.Play,Vector2.zero,new Vector2(920,102),()=>Show(MenuPage.MatchSetup),true);
                    NavigationCard("Multiplayer LAN",MenuIcon.Symbol.Network,new Vector2(0,-122),new Vector2(450,88),()=>Show(MenuPage.OnlineSetup));
                    NavigationCard("Guides",MenuIcon.Symbol.Book,new Vector2(470,-122),new Vector2(450,88),()=>OpenSubmenu(MenuPage.Guides));
                    NavigationCard("Settings",MenuIcon.Symbol.Settings,new Vector2(0,-228),new Vector2(450,88),()=>OpenSubmenu(MenuPage.Settings));
                    NavigationCard("Match history",MenuIcon.Symbol.History,new Vector2(470,-228),new Vector2(450,88),OpenHistory);
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
                    NavigationCard("Resume match",MenuIcon.Symbol.Play,Vector2.zero,new Vector2(1040,100),()=>Show(MenuPage.Actions),true);
                    NavigationCard("Guides",MenuIcon.Symbol.Book,new Vector2(0,-118),new Vector2(512,116),()=>OpenSubmenu(MenuPage.Guides));
                    NavigationCard("Settings",MenuIcon.Symbol.Settings,new Vector2(528,-118),new Vector2(512,116),()=>OpenSubmenu(MenuPage.Settings));
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
            var symbols=new[]{MenuIcon.Symbol.Controls,MenuIcon.Symbol.Audio,MenuIcon.Symbol.Visuals,MenuIcon.Symbol.Board,MenuIcon.Symbol.Language};
            for(int i=0;i<names.Length;i++)
            {
                var tab=(SettingsTab)i;bool active=settingsTab==tab;
                var rect=Section(actions,names[i],new Vector2(i*211,0),new Vector2(196,48),active?MenuPalette.Primary:button);
                Icon(rect,symbols[i],new Vector2(14,-14),20,active?accent:MenuMuted);
                Label(rect,names[i],new Vector2(40,0),new Vector2(146,48),18,active?MenuPalette.Ink:MenuMuted,TextAnchor.MiddleCenter).fontStyle=active?FontStyle.Bold:FontStyle.Normal;
                if(active)Section(rect,"Selected",new Vector2(20,-44),new Vector2(156,2),accent);
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
                var music=MenuCard("Music: "+Mathf.RoundToInt(AudioFeedback.MusicVolume*100)+"%","Background soundtrack · Select to change volume.",new Vector2(0,-70),new Vector2(512,174),()=>{AudioFeedback.CycleMusicVolume();Rebuild();});
                VolumeMeter(music,AudioFeedback.MusicVolume);
                var effects=MenuCard("Effects: "+Mathf.RoundToInt(AudioFeedback.EffectsVolume*100)+"%","Orders, combat and interface sounds · Select to change volume.",new Vector2(528,-70),new Vector2(512,174),()=>{AudioFeedback.CycleEffectsVolume();Rebuild();});
                VolumeMeter(effects,AudioFeedback.EffectsVolume);
            }
            else if(settingsTab==SettingsTab.Visuals)
            {
                MenuCard(Board.EnhancedLighting?"Lighting: Enhanced (experimental)":"Lighting: Current",
                    "Soft light, contact shadows and subtle highlights. Select to compare.",
                    new Vector2(0,-64),new Vector2(1040,76),()=>{Board.SetEnhancedLighting(!Board.EnhancedLighting);Rebuild();});
                int density=Mathf.RoundToInt(Board.FogDensity*100);
                Label(actions,"Fog density: "+density+"%"+(density==0?" (Off)":""),new Vector2(22,-156),new Vector2(996,32),23,Color.white);
                Label(actions,"Visual mist over unseen terrain. Unseen enemies stay hidden.",new Vector2(22,-192),new Vector2(996,28),17,MenuMuted);
                for(int i=0;i<=4;i++)
                {
                    int percent=i*25;bool active=density==percent;
                    string title=percent==0?"0% (Off)":percent+"%";
                    var rect=Section(actions,"Fog: "+title,new Vector2(i*211,-230),new Vector2(196,60),active?MenuPalette.Primary:button);
                    Label(rect,title,Vector2.zero,new Vector2(196,60),23,active?accent:Color.white,TextAnchor.MiddleCenter);
                    if(active)Section(rect,"Selected",new Vector2(20,-55),new Vector2(156,2),accent);
                    targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=()=>{Board.SetFogDensity(percent/100f);Rebuild();}});
                }
                Label(actions,"0% removes the mist. 100% is the original density.",new Vector2(22,-302),new Vector2(996,24),16,MenuMuted);
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

        private void VolumeMeter(RectTransform card,float volume)
        {
            for(int i=0;i<10;i++)
            {
                var segment=Section(card,"Volume segment",new Vector2(24+i*46,-136),new Vector2(40,14),MenuPalette.Surface);
                var surface=segment.GetComponent<MenuSurface>();surface.Style=MenuSurface.SurfaceStyle.Flat;surface.Radius=3;surface.BorderWidth=0;
                float fill=Mathf.Clamp01(volume*10-i);
                if(fill>0)
                {
                    var level=Section(segment,"Level",Vector2.zero,new Vector2(40*fill,14),accent).GetComponent<MenuSurface>();
                    level.Style=MenuSurface.SurfaceStyle.Flat;level.Radius=3;level.BorderWidth=0;
                }
            }
        }
    }
}
