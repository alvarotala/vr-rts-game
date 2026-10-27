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
        private static readonly Color MenuMuted=ConsoleTheme.Muted;
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
        private RectTransform MenuCard(string title,string description,Vector2 position,Vector2 size,Action click,bool primary=false,Color? fill=null)
        {
            var rect=Section(actions,title,position,size,fill??(primary?ConsoleTheme.Primary:button));
            bool compact=size.y<88,hero=primary&&size.y>200,tall=size.y>=200;
            float titleY=hero?-24:compact?-8:-16,descriptionY=hero?-96:tall?-76:compact?-41:-60;
            var heading=Label(rect,title,new Vector2(24,titleY),new Vector2(size.x-80,hero?50:32),hero?34:primary?28:compact?23:25,ConsoleTheme.Ink);
            heading.fontStyle=FontStyle.Bold;
            Label(rect,description,new Vector2(24,descriptionY),new Vector2(size.x-48,hero?100:compact?24:size.y+descriptionY-12),hero?21:tall?20:compact?17:18,MenuMuted,TextAnchor.UpperLeft);
            Label(rect,"›",new Vector2(size.x-48,hero?-size.y+65:-14),new Vector2(28,40),32,primary&&!fill.HasValue?accent:ConsoleTheme.Ink,TextAnchor.MiddleCenter);
            targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=click});
            return rect;
        }

        private void MenuFooter(string title,Action click,float x=0,float width=330,bool primary=false,float height=64,Color? fill=null)
        {
            var rect=Section(actions,title,new Vector2(x,-396+height),new Vector2(width,height),fill??(primary?ConsoleTheme.Primary:button));
            Label(rect,title,new Vector2(18,0),new Vector2(width-36,height),primary?28:23,ConsoleTheme.Ink,TextAnchor.MiddleCenter).fontStyle=primary?FontStyle.Bold:FontStyle.Normal;
            targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=click});
        }

        private void MainMenuTile(string title,ConsoleGlyph.Kind icon,int index,Action click,bool beta=false)
        {
            const float side=144,gap=20;
            float left=(1040-(side*4+gap*3))*.5f;
            var rect=Section(actions,title,new Vector2(left+index*(side+gap),-308),new Vector2(side,side),button);
            Glyph(rect,icon,new Vector2((side-44)*.5f,-14),44,ConsoleTheme.Ink);
            var label=Label(rect,title,new Vector2(8,-64),new Vector2(side-16,48),20,ConsoleTheme.Ink,TextAnchor.MiddleCenter);
            label.fontStyle=FontStyle.Bold;
            if(beta)
            {
                var badge=Section(rect,"Beta badge",new Vector2((side-64)*.5f,-122),new Vector2(64,18),WarningTint);
                ConsoleTheme.StylePill(badge.GetComponent<Image>());
                Label(badge,"BETA",Vector2.zero,new Vector2(64,18),13,ConsoleTheme.HoverInk,TextAnchor.MiddleCenter).fontStyle=FontStyle.Bold;
            }
            targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=button,click=click});
        }

        private void FactionCard(bool opponent,Vector2 position)
        {
            string faction=opponent?Session.EnemyFaction:Session.Faction;
            string title=(opponent?"Opponent: ":"Faction: ")+ShortFaction(faction);
            var rect=Section(actions,title,position,new Vector2(512,116),button);
            ModelPortrait(rect,new Vector2(20,-22),new Vector2(72,72),Session.Catalog.Role(faction,"hq"));
            Label(rect,title,new Vector2(112,-18),new Vector2(372,36),25,ConsoleTheme.Ink).fontStyle=FontStyle.Bold;
            Label(rect,opponent?"Enemy army · Select to switch faction.":"Your army · Select to switch faction.",
                new Vector2(112,-62),new Vector2(372,42),18,MenuMuted,TextAnchor.UpperLeft);
            targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=button,click=()=>
            {
                if(opponent)Session.EnemyFaction=Other(Session.EnemyFaction);else Session.Faction=Other(Session.Faction);
                Rebuild();
            }});
        }

        private bool BuildMenuPage()
        {
            switch(Page)
            {
                case MenuPage.Main:
                    MenuCard("New Match","Choose your army and map.\nPlay offline against the AI.",new Vector2(264,0),new Vector2(512,116),()=>Show(MenuPage.MatchSetup),true,ConsoleTheme.Secondary);
                    MenuCard("Settings","Controls, sound, visuals and play space.",new Vector2(264,-136),new Vector2(512,116),()=>OpenSubmenu(MenuPage.Settings));
                    MainMenuTile("Guided tutorial",ConsoleGlyph.Kind.Tutorial,0,()=>Quest.RequestTutorial());
                    MainMenuTile("Multiplayer",ConsoleGlyph.Kind.Group,1,()=>Show(MenuPage.OnlineSetup),true);
                    MainMenuTile("Guides",ConsoleGlyph.Kind.Guide,2,()=>OpenSubmenu(MenuPage.Guides));
                    MainMenuTile("Match history",ConsoleGlyph.Kind.History,3,OpenHistory);
                    return true;
                case MenuPage.History:BuildHistory();return true;
                case MenuPage.Replay:BuildReplayPanel();return true;
                case MenuPage.OnlineSetup:BuildOnlineSetup();return true;
                case MenuPage.OnlineConnection:BuildOnlineConnection();return true;
                case MenuPage.Ranking:BuildRanking();return true;
                case MenuPage.DirectSetup:BuildDirectSetup();return true;
                case MenuPage.PublicEndpoint:BuildOnlineEndpoint();return true;
                case MenuPage.OnlineEndpoint:BuildOnlineEndpoint();return true;
                case MenuPage.MatchSetup:
                    FactionCard(false,Vector2.zero);
                    FactionCard(true,new Vector2(528,0));
                    MenuCard("AI: "+DifficultyLabel,Session.Difficulty=="easy"?"More time to build and learn the controls.":Session.Difficulty=="hard"?"Fast economy and sustained pressure.":"Steady attacks, reinforcements and abilities.",new Vector2(0,-132),new Vector2(512,116),()=>{Session.Difficulty=Session.Difficulty=="easy"?"normal":Session.Difficulty=="normal"?"hard":"easy";Rebuild();});
                    MenuCard("Map: "+Session.Map.title,MapDescription,new Vector2(528,-132),new Vector2(512,116),()=>{Session.CycleMap();Board.Rebuild();Rebuild();});
                    MenuFooter("Back",Back,0,208);
                    MenuFooter("Position board",()=>{Quest.CancelStartRequest();PlaceRequested?.Invoke();},224,296);
                    MenuFooter("START MATCH",Quest.RequestStartMatch,536,504,true,80);
                    return true;
                case MenuPage.Pause:
                    MenuCard("Resume match",TutorialResumeBriefing?"Choose Continue in the floating guide first.":"Return to the battlefield.",Vector2.zero,new Vector2(1040,116),()=>Show(MenuPage.Actions),!TutorialResumeBriefing);
                    MenuCard("Guides","Controls, units and structures.",new Vector2(0,-132),new Vector2(512,144),()=>OpenSubmenu(MenuPage.Guides));
                    MenuCard("Settings","Controls, sound, visuals and play space.",new Vector2(528,-132),new Vector2(512,144),()=>OpenSubmenu(MenuPage.Settings));
                    MenuFooter("Return to main menu",()=>Show(MenuPage.LeaveConfirmation),0,512);
                    return true;
                case MenuPage.Guides:
                    MenuCard("Control guide","Selection, orders and board gestures.",Vector2.zero,new Vector2(336,280),()=>OpenSubmenu(MenuPage.Guide));
                    MenuCard("Units","Costs, combat and abilities.",new Vector2(352,0),new Vector2(336,280),()=>OpenSubmenu(MenuPage.UnitsGuide));
                    MenuCard("Structures","Construction, requirements and production.",new Vector2(704,0),new Vector2(336,280),()=>OpenSubmenu(MenuPage.StructuresGuide));
                    MenuFooter("Back",Back);return true;
                case MenuPage.Settings:
                    BuildSettingsPage();return true;
                case MenuPage.LeaveConfirmation:
                    MenuCard("Keep playing","Return to the pause menu.",Vector2.zero,new Vector2(512,140),()=>Show(MenuPage.Pause),true,ConsoleTheme.Success);
                    MenuCard("Leave match",Session.IsOnline?"Concede to your opponent and return to the main menu.":"End this match and return to the main menu.",new Vector2(528,0),new Vector2(512,140),()=>Session.LeaveMatch(),true,ConsoleTheme.Danger);
                    return true;
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
                var rect=Section(actions,names[i],new Vector2(i*211.2f,0),new Vector2(195.2f,52),active?ConsoleTheme.Primary:button);
                Label(rect,names[i],Vector2.zero,new Vector2(195.2f,52),20,ConsoleTheme.Ink,TextAnchor.MiddleCenter).fontStyle=active?FontStyle.Bold:FontStyle.Normal;
                targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=()=>{settingsTab=tab;Rebuild();UpdateMenuText();}});
            }
            if(settingsTab==SettingsTab.Controls)
            {
                MenuCard("Hand: "+Bindings.HandName,"Switch the active controller and its button hints.",new Vector2(0,-68),new Vector2(512,116),ToggleControllerHand);
                MenuCard("Hotkeys","Customize gameplay buttons and keyboard shortcuts.",new Vector2(528,-68),new Vector2(512,116),()=>OpenSubmenu(MenuPage.Hotkeys));
                MenuCard("Selection: "+(Board.BaseTargeting?"Bases":"Bodies"),Board.BaseTargeting?"Aim at ground areas. Units take priority over buildings and resources.":"Aim at models or ground areas. Units take priority over buildings and resources.",new Vector2(0,-200),new Vector2(1040,116),()=>{Board.SetBaseTargeting(!Board.BaseTargeting);Rebuild();});
            }
            else if(settingsTab==SettingsTab.Audio)
            {
                var music=MenuCard("Music: "+Mathf.RoundToInt(AudioFeedback.MusicVolume*100)+"%","Background soundtrack · Select to change volume.",new Vector2(0,-68),new Vector2(512,232),()=>{AudioFeedback.CycleMusicVolume();Rebuild();});
                VolumeMeter(music,AudioFeedback.MusicVolume);
                var effects=MenuCard("Effects: "+Mathf.RoundToInt(AudioFeedback.EffectsVolume*100)+"%","Orders, combat and interface sounds · Select to change volume.",new Vector2(528,-68),new Vector2(512,232),()=>{AudioFeedback.CycleEffectsVolume();Rebuild();});
                VolumeMeter(effects,AudioFeedback.EffectsVolume);
            }
            else if(settingsTab==SettingsTab.Visuals)
            {
                MenuCard(Board.EnhancedLighting?"Lighting: Enhanced (experimental)":"Lighting: Current",
                    "Soft light, contact shadows and subtle highlights. Select to compare.",
                    new Vector2(0,-64),new Vector2(1040,76),()=>{Board.SetEnhancedLighting(!Board.EnhancedLighting);Rebuild();});
                int density=Mathf.RoundToInt(Board.FogDensity*100);
                Label(actions,"Fog density: "+density+"%"+(density==0?" (Off)":""),new Vector2(24,-156),new Vector2(992,32),23,ConsoleTheme.Ink);
                Label(actions,"Visual mist over unseen terrain. Unseen enemies stay hidden.",new Vector2(24,-192),new Vector2(992,28),17,MenuMuted);
                for(int i=0;i<=4;i++)
                {
                    int percent=i*25;bool active=density==percent;
                    string title=percent==0?"0% (Off)":percent+"%";
                    var rect=Section(actions,"Fog: "+title,new Vector2(i*211.2f,-230),new Vector2(195.2f,60),active?ConsoleTheme.Primary:button);
                    Label(rect,title,Vector2.zero,new Vector2(195.2f,60),23,ConsoleTheme.Ink,TextAnchor.MiddleCenter).fontStyle=active?FontStyle.Bold:FontStyle.Normal;
                    targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=()=>{Board.SetFogDensity(percent/100f);Rebuild();}});
                }
                Label(actions,"0% removes the mist. 100% is the densest, darkest mist.",new Vector2(24,-302),new Vector2(992,24),16,MenuMuted);
            }
            else if(settingsTab==SettingsTab.Language)
            {
                Label(actions,"Language",new Vector2(24,-76),new Vector2(992,36),26,ConsoleTheme.Ink).fontStyle=FontStyle.Bold;
                Label(actions,"Use your system language or choose one for this device. Changes apply immediately.",new Vector2(24,-122),new Vector2(992,48),18,MenuMuted);
                // One selector enumerates the installed catalogs, with no fixed language limit.
                string choice=Localization.Preference==Localization.SystemPreference?"System · "+Localization.CurrentName:Localization.CurrentName;
                MenuCard("Language: "+choice,"Select to change · System follows the device language.",new Vector2(0,-188),new Vector2(1040,116),()=>
                {
                    var options=new System.Collections.Generic.List<string>{Localization.SystemPreference};
                    foreach(var language in Localization.Languages)options.Add(language.code);
                    Localization.SetPreference(options[(options.IndexOf(Localization.Preference)+1)%options.Count]);
                    Rebuild();UpdateMenuText();
                });
            }
            else
            {
                MenuCard("Reposition board","Place your battlefield and adjust its height as you aim.",new Vector2(0,-68),new Vector2(512,116),()=>PlaceRequested?.Invoke());
                MenuCard("Detect surfaces","Refresh detected room surfaces for placement.",new Vector2(528,-68),new Vector2(512,116),()=>Quest.LoadRoom());
                Label(actions,"Adjust while placing",new Vector2(24,-204),new Vector2(992,34),24,ConsoleTheme.Ink).fontStyle=FontStyle.Bold;
                Label(actions,Quest.Active
                    ?"Grip: shrink · Trigger: enlarge · "+Bindings.MenuConfirmName+": confirm · "+Bindings.MenuBackName+": cancel\nStick ↑↓: height · ←→: rotate · Stick click: show / hide hints"
                    :"Page Up / Page Down: raise / lower · Left-click: confirm · Escape: cancel\nTap for a small height change, or hold to keep adjusting.",
                    new Vector2(24,-248),new Vector2(992,64),18,MenuMuted);
            }
            MenuFooter("Back",Back);
        }

        private void VolumeMeter(RectTransform card,float volume)
        {
            const float width=464;
            var track=Section(card,"Volume",new Vector2(24,-192),new Vector2(width,16),ConsoleTheme.Track);
            float fill=Mathf.Clamp01(volume);
            if(fill>0)Section(track,"Level",Vector2.zero,new Vector2(width*fill,16),accent);
        }
    }
}
