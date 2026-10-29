#if FRONTIER_BROWSER
using System;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // Touch presentation shares every action with the existing session. Coordinates
    // stay in canvas units; the safe area also protects browser landscape notches.
    public sealed partial class CommandPanel
    {
        private RectTransform mobileToolbar;
        private Target mobileClear;
        private bool mobileLayoutApplied,mobileReplacingGroup;
        private object mobileGroupMatch;
        private int[] mobileGroupSelection=Array.Empty<int>();
        private readonly SelectionPortrait[] mobileGroupPortraits=new SelectionPortrait[GameSession.ControlGroupCount];
        private readonly ConsoleGlyph[] mobileGroupGlyphs=new ConsoleGlyph[GameSession.ControlGroupCount];
        private Vector2 mobileActionPosition,mobileActionSize;
        private const float MobileActionHeight=88;
        private const string MobileGroupReplaceMessage="All four groups are occupied. Tap a group icon to replace it, or Cancel.";
        private const string MobileTutorialGroupReplaceMessage="Group 1 already contains a squad. Tap group 1 to replace it with your selected soldiers, or Cancel.";
        private string MobileGroupReplacementPrompt=>Session.IsTutorial&&Session.Tutorial.Step==TutorialStep.SaveGroup
            ?MobileTutorialGroupReplaceMessage:MobileGroupReplaceMessage;
        private bool IsMobileMenu=>Page==MenuPage.Main||Page==MenuPage.MatchSetup||Page==MenuPage.Pause
            ||Page==MenuPage.Settings||Page==MenuPage.LeaveConfirmation||Page==MenuPage.Guide||Page==MenuPage.Guides
            ||Page==MenuPage.OnlineSetup||Page==MenuPage.OnlineConnection||Page==MenuPage.DirectSetup;

        private void ConfigureMobileCanvas()
        {
            var scaler=canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution=BrowserMobileInput.IsTouchMode
                ?Screen.width<Screen.height?new Vector2(720,1280):new Vector2(1280,720):new Vector2(1600,900);
        }

        private static Rect BrowserSafeArea(Vector2 size)
        {
            if(Screen.width<=0||Screen.height<=0)return new Rect(Vector2.zero,size);
            var safe=Screen.safeArea;
            if(safe.width<=0||safe.height<=0)return new Rect(Vector2.zero,size);
            return new Rect(safe.x/Screen.width*size.x,safe.y/Screen.height*size.y,
                safe.width/Screen.width*size.x,safe.height/Screen.height*size.y);
        }

        private void CreateMobileHud()
        {
            mobileToolbar=Section(browserHud,"Touch selection controls",Vector2.zero,new Vector2(720,80),ConsoleTheme.Background);
            mobileClear=BrowserButton(mobileToolbar,"Cancel / clear",Vector2.zero,new Vector2(160,72),()=>
            {
                if(mobileReplacingGroup){CancelMobileGroupReplacement();Rebuild();return;}
                Session.CancelMode();Session.Select(0,false);
            });
            for(int i=0;i<groupTargets.Length;i++)
            {
                mobileGroupGlyphs[i]=Glyph(groupTargets[i].rect,ConsoleGlyph.Kind.Group,new Vector2(8,-14),44,ConsoleTheme.Muted);
                mobileGroupPortraits[i]=ModelPortrait(groupTargets[i].rect,new Vector2(8,-14),new Vector2(44,44));
                mobileGroupGlyphs[i].gameObject.SetActive(false);mobileGroupPortraits[i].gameObject.SetActive(false);
            }
            mobileToolbar.gameObject.SetActive(false);
        }

        private void LayoutMobileHud(Vector2 size)
        {
            mobileLayoutApplied=true;
            var safe=BrowserSafeArea(size);bool portrait=safe.height>safe.width;
            float left=safe.x+8,top=size.y-safe.yMax+8,width=safe.width-16,bottom=size.y-safe.y+8;
            float dockHeight=portrait?480:244,dockTop=bottom-dockHeight-16,toolsTop=dockTop-88;
            root.localScale=Vector3.one;root.sizeDelta=size;root.GetComponent<Image>().color=Color.clear;
            TopLeft(browserHud,Vector2.zero,size);
            TopLeft(browserBar,new Vector2(left,-top),new Vector2(width,80));
            LayoutMobileButton(browserButtons[0],new Vector2(4,-4),new Vector2(104,72));
            var resources=new[]{browserMinerals,browserGas,browserSupply};
            var glyphs=browserBar.GetComponentsInChildren<ConsoleGlyph>();
            float resourceWidth=(width-124-(portrait?0:100))/3;
            for(int i=0;i<resources.Length;i++)
            {
                float x=120+i*resourceWidth;
                if(i<glyphs.Length)TopLeft(glyphs[i].rectTransform,new Vector2(x,-28),new Vector2(24,24));
                TopLeft(resources[i].rectTransform,new Vector2(x+30,-10),new Vector2(resourceWidth-34,60));resources[i].fontSize=24;
            }
            browserClock.gameObject.SetActive(!portrait);
            TopLeft(browserClock.rectTransform,new Vector2(width-104,-10),new Vector2(92,60));

            mobileToolbar.gameObject.SetActive(true);
            TopLeft(mobileToolbar,new Vector2(left,-toolsTop),new Vector2(width,80));
            browserButtons[1].rect.SetParent(mobileToolbar,false);browserButtons[2].rect.SetParent(mobileToolbar,false);
            var tools=new[]{browserButtons[1],browserButtons[2],mobileClear};
            float quickWidth=width*.43f,toolWidth=(quickWidth-16)/3,groupsWidth=width-quickWidth-8,groupWidth=(groupsWidth-12)/4;
            for(int i=0;i<tools.Length;i++)LayoutMobileButton(tools[i],new Vector2(4+i*(toolWidth+4),-4),new Vector2(toolWidth,72));
            foreach(var target in tools){target.rect.gameObject.SetActive(true);target.rect.GetComponentInChildren<Text>().fontSize=portrait?17:21;}
            mobileClear.rect.GetComponentInChildren<Text>().text=mobileReplacingGroup?"Cancel":"Cancel / clear";
            browserGroupBar.gameObject.SetActive(true);
            TopLeft(browserGroupBar,new Vector2(left+quickWidth,-toolsTop-4),new Vector2(groupsWidth,72));
            for(int i=0;i<groupTargets.Length;i++)
            {
                var target=groupTargets[i];LayoutMobileButton(target,new Vector2(i*(groupWidth+4),0),new Vector2(groupWidth,72));
                TopLeft(target.rect.GetComponentInChildren<Text>().rectTransform,new Vector2(groupWidth-34,-4),new Vector2(28,28));
                TopLeft(groupCounts[i].rectTransform,new Vector2(groupWidth-38,-38),new Vector2(32,26));groupCounts[i].fontSize=17;
                float iconSize=Mathf.Min(44,groupWidth-44);
                TopLeft(mobileGroupGlyphs[i].rectTransform,new Vector2(6,-(72-iconSize)*.5f),new Vector2(iconSize,iconSize));
                TopLeft(mobileGroupPortraits[i].rectTransform,new Vector2(6,-(72-iconSize)*.5f),new Vector2(iconSize,iconSize));
            }

            float mapSide=portrait?144:180,infoWidth=portrait?width-mapSide-8:Mathf.Max(244,width*.25f);
            TopLeft(browserMapCard,new Vector2(left,-dockTop),new Vector2(mapSide,mapSide));
            TopLeft(browserMinimap.rectTransform,new Vector2(6,-6),new Vector2(mapSide-12,mapSide-12));
            TopLeft(browserInfo,new Vector2(left+mapSide+8,-dockTop),new Vector2(infoWidth,portrait?144:244));
            browserPortrait.gameObject.SetActive(false);
            TopLeft(browserName.rectTransform,new Vector2(12,-8),new Vector2(infoWidth-24,32));browserName.fontSize=24;
            TopLeft(browserHealth.rectTransform,new Vector2(12,-42),new Vector2(infoWidth-24,26));browserHealth.fontSize=19;
            TopLeft(browserDescription.rectTransform,new Vector2(12,-72),new Vector2(infoWidth-24,portrait?56:48));browserDescription.fontSize=18;
            browserQueue.gameObject.SetActive(true);browserQueueTitle.gameObject.SetActive(true);
            browserQueue.SetParent(portrait?browserCommandCard:browserInfo,false);
            browserQueueTitle.transform.SetParent(portrait?browserCommandCard:browserInfo,false);
            TopLeft(browserQueueTitle.rectTransform,new Vector2(12,-128),new Vector2(infoWidth-24,40));browserQueueTitle.fontSize=16;
            TopLeft(browserQueue,new Vector2(12,-174),new Vector2(infoWidth-24,64));

            float commandX=portrait?left:left+mapSide+infoWidth+24,commandTop=portrait?dockTop+152:dockTop;
            float commandWidth=portrait?width:left+width-commandX;
            TopLeft(browserCommandCard,new Vector2(commandX,-commandTop),new Vector2(commandWidth,portrait?328:244));
            if(portrait)
            {
                TopLeft(browserQueue,new Vector2(8,-246),new Vector2(commandWidth-16,72));
                TopLeft(browserQueueTitle.rectTransform,new Vector2(8,-222),new Vector2(commandWidth-16,22));
            }
            TopLeft(browserCommandTitle.rectTransform,new Vector2(12,-6),new Vector2(commandWidth-24,28));browserCommandTitle.fontSize=18;
            mobileActionPosition=new Vector2(commandX+8,-commandTop-38);
            mobileActionSize=new Vector2(commandWidth-16,portrait?248:198);
            TopLeft(actions,mobileActionPosition,mobileActionSize);
            TopLeft(statusBanner,new Vector2(left+8,-top-88),new Vector2(width-16,60));
            status.fontSize=21;status.alignment=TextAnchor.MiddleCenter;
            TopLeft(status.rectTransform,Vector2.zero,statusBanner.sizeDelta);
            float viewportBottom=(size.y-toolsTop+8)/size.y,viewportTop=(size.y-top-88)/size.y;
            Quest.Navigation?.SetViewport(new Rect(safe.x/size.x,viewportBottom,safe.width/size.x,Mathf.Max(.15f,viewportTop-viewportBottom)));
            if(browserScreenSize!=size){browserScreenSize=size;browserQueueSignature=null;}
        }

        private static void LayoutMobileButton(Target target,Vector2 position,Vector2 size)
        {
            TopLeft(target.rect,position,size);
            var title=target.rect.GetComponentInChildren<Text>();
            TopLeft(title.rectTransform,new Vector2(6,0),size-new Vector2(12,0));title.fontSize=21;
        }

        private void BuildMobileActions()
        {
            targets.AddRange(browserButtons);targets.Add(mobileClear);targets.AddRange(groupTargets);
            var available=SelectionActions.For(Session);
            ValidateMobileGroupReplacement();
            bool canGroup=Session.CanSaveMobileGroup;
            string context=string.Join(",",Session.Selection.OrderBy(id=>id))+":"+string.Join(",",available.Items.Select(a=>a.Id))+":"+canGroup+":"+mobileReplacingGroup;
            if(context!=browserActionContext){browserActionContext=context;browserActionPage=0;}
            const int pageSize=5;
            int itemCount=available.Items.Count+(canGroup?1:0);
            int pages=Mathf.Max(1,(itemCount+pageSize-1)/pageSize);
            browserActionPage=Mathf.Clamp(browserActionPage,0,pages-1);
            browserCommandTitle.text=available.Heading;
            float width=(mobileActionSize.x-16)/3;
            for(int slot=0;slot<pageSize;slot++)
            {
                int index=browserActionPage*pageSize+slot;if(index>=itemCount)break;
                var position=new Vector2(slot%3*(width+8),-slot/3*(MobileActionHeight+8));
                if(canGroup&&index==0)
                {
                    var group=BrowserButton(actions,mobileReplacingGroup?"Cancel replacement":"Group selection",position,new Vector2(width,MobileActionHeight),()=>
                    {
                        if(mobileReplacingGroup)CancelMobileGroupReplacement();else SaveMobileGroup();
                        Rebuild();
                    });
                    group.actionId="mobile-group";group.normal=mobileReplacingGroup?ConsoleTheme.Danger:ConsoleTheme.Primary;group.image.color=group.normal;
                    group.rect.GetComponentInChildren<Text>().fontSize=22;targets.Add(group);continue;
                }
                if(canGroup)index--;
                var action=available.Items[index];
                var target=HudButton(actions,action.Label,MatchActionDetail(action,false),position,
                    new Vector2(width,MobileActionHeight),()=>{CancelMobileGroupReplacement();action.Execute();},null,out var detail);
                target.actionId=action.Id;target.help=action;
                bool active=action.Building&&action.Id=="build:"+Session.BuildDefinition||action.Id=="attack-move"&&Session.AttackMoveMode||action.Id=="set-rally-point"&&Session.RallyPointMode;
                target.normal=active?ConsoleTheme.Primary:Color.Lerp(button,ActionTint(action.Category).linear,.16f);target.image.color=target.normal;
                var title=target.rect.GetComponentInChildren<Text>();title.fontSize=20;title.fontStyle=FontStyle.Bold;
                TopLeft(title.rectTransform,new Vector2(8,-6),new Vector2(width-16,44));
                title.resizeTextForBestFit=true;title.resizeTextMinSize=17;title.resizeTextMaxSize=20;
                TopLeft(detail.rectTransform,new Vector2(8,-54),new Vector2(width-16,26));detail.fontSize=17;
                targets.Add(target);
            }
            if(pages>1)
            {
                var next=BrowserButton(actions,(browserActionPage+1)+" / "+pages+"  ›",
                    new Vector2(2*(width+8),-MobileActionHeight-8),new Vector2(width,MobileActionHeight),()=>{browserActionPage=(browserActionPage+1)%pages;Rebuild();});
                next.rect.GetComponentInChildren<Text>().fontSize=22;
                targets.Add(next);
            }
            browserQueueSignature=null;RefreshBrowserQueues();UpdateBrowserHud();
        }

        private void SaveMobileGroup()
        {
            if(!AcceptsGameInput||!Session.CanSaveMobileGroup)return;
            int saved=Session.SaveMobileSelectionGroup();
            if(saved>=0){ConfirmMobileGroup(saved);return;}
            mobileReplacingGroup=true;mobileGroupMatch=Session.SessionIdentity;mobileGroupSelection=Session.Selection.ToArray();
            Session.Announce(MobileGroupReplacementPrompt);
        }

        private void CancelMobileGroupReplacement()
        {
            if(!mobileReplacingGroup)return;
            mobileReplacingGroup=false;mobileGroupMatch=null;mobileGroupSelection=Array.Empty<int>();
            if(Session.Message==MobileGroupReplaceMessage||Session.Message==MobileTutorialGroupReplaceMessage)
            {browserMessageUntil=0;browserMessageVersion=Session.MessageVersion;}
            browserActionContext=null;signature=null;
        }

        private void ValidateMobileGroupReplacement()
        {
            if(mobileReplacingGroup&&(!AcceptsGameInput||mobileGroupMatch!=Session.SessionIdentity
                ||!Session.CanSaveMobileGroup||!Session.Selection.SetEquals(mobileGroupSelection)))CancelMobileGroupReplacement();
        }

        private void ConfirmMobileGroup(int index)
        {
            CancelMobileGroupReplacement();groupConfirmationMatch=Session.SessionIdentity;
            groupConfirmedUntil[index]=Time.unscaledTime+GroupConfirmationDuration;
            Session.GetComponent<AudioFeedback>()?.Navigation(true);
        }

        private void HandleMobileGroupTap(int index)
        {
            bool replacing=mobileReplacingGroup;ValidateMobileGroupReplacement();
            if(replacing)
            {
                if(mobileReplacingGroup&&Session.ReplaceMobileSelectionGroup(index))ConfirmMobileGroup(index);
                CancelMobileGroupReplacement();Rebuild();return;
            }
            Session.GetComponent<AudioFeedback>()?.Navigation(true);Session.RecallMobileGroup(index);
        }

        private void RenderMobileGroupFeedback()
        {
            ValidateMobileGroupReplacement();
            for(int index=0;index<groupTargets.Length;index++)
            {
                var target=groupTargets[index];if(target==null||target.image==null||mobileGroupGlyphs[index]==null)continue;
                int count=Session.GroupSize(index);string definition=Session.MobileGroupDefinition(index);
                bool hasModel=count>0&&!string.IsNullOrEmpty(definition)&&Session.Catalog.Has(definition);
                mobileGroupGlyphs[index].gameObject.SetActive(!hasModel);mobileGroupPortraits[index].gameObject.SetActive(hasModel);
                if(hasModel)mobileGroupPortraits[index].SetType(Session.Catalog[definition]);
                bool confirmed=groupConfirmationMatch==Session.SessionIdentity&&Time.unscaledTime<groupConfirmedUntil[index];
                target.normal=mobileReplacingGroup?ConsoleTheme.Danger:Session.MobileGroupMatchesSelection(index)?ConsoleTheme.Primary:button;
                ApplyTargetHover(target,hovered==target,confirmed?QueueActiveTint:target.normal);
                groupCounts[index].text=count==0?"–":count.ToString();
            }
        }

        private void RestoreDesktopHud()
        {
            if(!mobileLayoutApplied)return;mobileLayoutApplied=false;
            mobileToolbar.gameObject.SetActive(false);browserGroupBar.gameObject.SetActive(true);
            browserClock.gameObject.SetActive(true);browserQueue.gameObject.SetActive(true);browserQueueTitle.gameObject.SetActive(true);
            browserQueue.SetParent(browserInfo,false);
            browserQueueTitle.transform.SetParent(browserInfo,false);
            browserButtons[1].rect.SetParent(browserBar,false);browserButtons[2].rect.SetParent(browserBar,false);
            var positions=new[]{new Vector2(12,-10),new Vector2(624,-10),new Vector2(784,-10)};
            var widths=new[]{104f,150f,130f};
            for(int i=0;i<browserButtons.Count;i++)
            {
                browserButtons[i].rect.gameObject.SetActive(true);
                LayoutMobileButton(browserButtons[i],positions[i],new Vector2(widths[i],40));
                browserButtons[i].rect.GetComponentInChildren<Text>().fontSize=17;
            }
            var resources=new[]{browserMinerals,browserGas,browserSupply};var glyphs=browserBar.GetComponentsInChildren<ConsoleGlyph>();
            var x=new[]{130f,294f,444f};
            for(int i=0;i<resources.Length;i++)
            {
                TopLeft(glyphs[i].rectTransform,new Vector2(x[i],-19),new Vector2(24,24));
                TopLeft(resources[i].rectTransform,new Vector2(x[i]+34,-10),new Vector2(120,40));resources[i].fontSize=23;
            }
            for(int i=0;i<groupTargets.Length;i++)
            {
                mobileGroupGlyphs[i].gameObject.SetActive(false);mobileGroupPortraits[i].gameObject.SetActive(false);
                groupTargets[i].normal=button;
                TopLeft(groupTargets[i].rect,new Vector2(i*146,0),new Vector2(138,38));
                TopLeft(groupTargets[i].rect.GetComponentInChildren<Text>().rectTransform,new Vector2(6,0),new Vector2(24,38));
                groupTargets[i].rect.GetComponentInChildren<Text>().fontSize=17;
                TopLeft(groupCounts[i].rectTransform,new Vector2(36,0),new Vector2(96,38));groupCounts[i].fontSize=14;
            }
            TopLeft(browserMinimap.rectTransform,new Vector2(10,-10),new Vector2(224,224));
            browserName.fontSize=24;browserHealth.fontSize=17;browserDescription.fontSize=16;browserQueueTitle.fontSize=14;browserCommandTitle.fontSize=16;
            browserQueue.anchoredPosition=new Vector2(16,-170);
        }

        private void LayoutMobileMenu(Vector2 size)
        {
            var safe=BrowserSafeArea(size);root.localScale=Vector3.one;root.sizeDelta=safe.size-new Vector2(16,16);
            root.anchoredPosition=safe.center-size*.5f;
            root.GetComponent<Image>().color=background;
            consoleBrand.gameObject.SetActive(false);consoleBrandSmall.gameObject.SetActive(false);
            headline.gameObject.SetActive(true);headline.fontSize=34;
            TopLeft(headline.rectTransform,new Vector2(24,-20),new Vector2(root.rect.width-48,48));
            economy.gameObject.SetActive(true);economy.fontSize=20;economy.alignment=TextAnchor.MiddleLeft;
            TopLeft(economy.rectTransform,new Vector2(24,-76),new Vector2(root.rect.width-48,32));
            selection.gameObject.SetActive(false);harvesting.gameObject.SetActive(false);
            TopLeft(actions,new Vector2(24,-124),new Vector2(root.rect.width-48,root.rect.height-210));
            TopLeft(statusBanner,new Vector2(24,-root.rect.height+72),new Vector2(root.rect.width-48,56));
            TopLeft(status.rectTransform,Vector2.zero,statusBanner.sizeDelta);status.fontSize=19;
        }

        private bool BuildMobileMenu()
        {
            if(!IsMobileMenu)return false;
            controlGuide.gameObject.SetActive(false);hotkeys.gameObject.SetActive(false);
            BuildReferenceGuide();BuildTutorialCard();
            float width=actions.rect.width;bool portrait=root.rect.height>root.rect.width;
            int columns=portrait?1:2;
            float cellWidth=(width-(columns-1)*12)/columns;
            int index=0;
            Action<string,string,Action> card=(title,description,click)=>
            {
                int item=index++;const float height=104;
                var rect=Section(actions,title,new Vector2(item%columns*(cellWidth+12),-item/columns*(height+12)),new Vector2(cellWidth,height),button);
                Label(rect,title,new Vector2(16,-10),new Vector2(cellWidth-32,34),26,ConsoleTheme.Ink).fontStyle=FontStyle.Bold;
                Label(rect,description,new Vector2(16,-50),new Vector2(cellWidth-32,46),19,ConsoleTheme.Muted,TextAnchor.UpperLeft);
                targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=button,click=click});
            };
            switch(Page)
            {
                case MenuPage.Main:
                    card("New Match","Choose your army and map.",()=>Show(MenuPage.MatchSetup));
                    card("Multiplayer","Find an online opponent.",()=>Show(MenuPage.OnlineSetup));
                    card("Guided tutorial","Learn to command your army.",()=>Quest.RequestTutorial());
                    card("Settings","Sound, visuals and language.",()=>OpenSubmenu(MenuPage.Settings));
                    card("Guides","Touch controls, units and structures.",()=>OpenSubmenu(MenuPage.Guides));
                    card("Match history","Review your matches and replays.",OpenHistory);
                    break;
                case MenuPage.MatchSetup:
                    card("Faction: "+ShortFaction(Session.Faction),"Your army · Select to switch faction.",()=>{Session.Faction=Other(Session.Faction);Rebuild();});
                    card("Opponent: "+ShortFaction(Session.EnemyFaction),"Enemy army · Select to switch faction.",()=>{Session.EnemyFaction=Other(Session.EnemyFaction);Rebuild();});
                    card("AI: "+DifficultyLabel,"Select to change difficulty.",()=>{Session.Difficulty=Session.Difficulty=="easy"?"normal":Session.Difficulty=="normal"?"hard":"easy";Rebuild();});
                    card("Map: "+Session.Map.title,Session.Map.width+" × "+Session.Map.height+" · 1v1",()=>{Session.CycleMap();Board.Rebuild();Rebuild();});
                    card("START MATCH","Play offline against the AI.",Quest.RequestStartMatch);
                    card("Back","Return to main menu",Back);break;
                case MenuPage.Pause:
                    card("Resume match","Return to the battlefield.",()=>Show(MenuPage.Actions));
                    card("Guides","Touch controls, units and structures.",()=>OpenSubmenu(MenuPage.Guides));
                    card("Settings","Sound, visuals and language.",()=>OpenSubmenu(MenuPage.Settings));
                    card("Full map","Center the camera on the battlefield.",()=>Quest.Navigation.Fit());
                    card("Return to main menu","Leave this match.",()=>Show(MenuPage.LeaveConfirmation));break;
                case MenuPage.OnlineSetup:
                    if(Session.IsConnecting)
                    {
                        card(Session.IsMatchmaking?"Finding an opponent":"Opponent found","Keep this app open while your match is prepared.",()=>{});
                        card("Cancel search","Return to main menu",CancelOnlineSetup);break;
                    }
                    card("Faction: "+ShortFaction(Session.Faction),"Your army · Select to switch faction.",()=>{Session.Faction=Other(Session.Faction);Rebuild();});
                    card("FIND MATCH","Find an opponent near your rating. The range widens while you wait.",()=>{endpointMessage=null;Quest.RequestPublicMatch();Rebuild();});
                    card("Ranking","Your rating and the top 10 players.",()=>Show(MenuPage.Ranking));
                    card("Connection","Online service and direct LAN connection.",()=>Show(MenuPage.OnlineConnection));
                    card("Back","Return to main menu",CancelOnlineSetup);break;
                case MenuPage.OnlineConnection:
                    var service=PublicBackend();
                    card("Online service",service.Host,()=>EditOnlineEndpoint(false,true));
                    card("Service port: "+service.Port,"Use the port of your online service.",()=>EditOnlineEndpoint(true,true));
                    card("Direct connection","Join a server by address on your local network.",()=>Show(MenuPage.DirectSetup));
                    card("Back","Return to the previous menu.",()=>Show(MenuPage.OnlineSetup));break;
                case MenuPage.DirectSetup:
                    if(Session.IsConnecting)
                    {
                        card("Waiting for the server and your opponent","Keep this app open. Both players need to join the same server.",()=>{});
                        card("Cancel connection","Return to main menu",CancelOnlineSetup);break;
                    }
                    card("Server: "+Session.ServerAddress,"Enter the IPv4 address of the computer running the server.",()=>EditOnlineEndpoint(false));
                    card("Port: "+Session.ServerPort,"Use the port configured on the server.",()=>EditOnlineEndpoint(true));
                    card("JOIN MATCH","The server chooses the map and assigns each player's faction.",()=>{endpointMessage=null;Quest.RequestOnlineMatch();Rebuild();});
                    card("Back","Return to the previous menu.",()=>{Quest.CancelStartRequest();Show(MenuPage.OnlineSetup);});break;
                case MenuPage.LeaveConfirmation:
                    card("Keep playing","Return to the pause menu.",()=>Show(MenuPage.Pause));
                    card("Leave match",Session.IsOnline?"Concede to your opponent and return to the main menu.":"End this match and return to the main menu.",()=>Session.LeaveMatch());break;
                case MenuPage.Settings:
                    card("Music: "+Mathf.RoundToInt(AudioFeedback.MusicVolume*100)+"%","Select to change volume.",()=>{AudioFeedback.CycleMusicVolume();Rebuild();});
                    card("Effects: "+Mathf.RoundToInt(AudioFeedback.EffectsVolume*100)+"%","Select to change volume.",()=>{AudioFeedback.CycleEffectsVolume();Rebuild();});
                    card("Language: "+Localization.CurrentName,"Select to change the interface language.",()=>
                    {
                        var options=new[]{Localization.SystemPreference}.Concat(Localization.Languages.Select(l=>l.code)).ToArray();
                        Localization.SetPreference(options[(Array.IndexOf(options,Localization.Preference)+1)%options.Length]);Rebuild();UpdateMenuText();
                    });
                    card("Selection: "+(Board.BaseTargeting?"Bases":"Bodies"),"Select to change unit targeting.",()=>{Board.SetBaseTargeting(!Board.BaseTargeting);Rebuild();});
                    card("Fog density: "+Mathf.RoundToInt(Board.FogDensity*100)+"%","Visual mist over unseen terrain. Unseen enemies stay hidden.",()=>{Board.SetFogDensity(Board.FogDensity>=1?0:Board.FogDensity+.25f);Rebuild();});
                    card("Back","Return to the previous menu.",Back);break;
                case MenuPage.Guides:
                    card("Control guide","Circle units. Use two fingers to move the camera.",()=>OpenSubmenu(MenuPage.Guide));
                    card("Units","Costs, combat and abilities.",()=>OpenSubmenu(MenuPage.UnitsGuide));
                    card("Structures","Construction, requirements and production.",()=>OpenSubmenu(MenuPage.StructuresGuide));
                    card("Back","Return to the previous menu.",Back);break;
                case MenuPage.Guide:
                    card("Select","Tap an ally. Double-tap selects its type.",()=>{});
                    card("Order","Tap a destination. Hold and release on an ally to repair or assist.",()=>{});
                    card("Camera","Drag with two fingers to pan. Pinch to zoom. Tap the minimap to travel.",()=>{});
                    card("Circle units","Draw around your units with one finger, then release to select them.",()=>{});
                    card("Groups / actions","Tap Group selection to save. Tap its persistent icon to select the group.",()=>{});
                    card("Back","Return to the previous menu.",Back);break;
            }
            return true;
        }

        private string MobileTutorialInstruction(string key)
        {
            switch(key)
            {
                case "Click Continue. Let's play!":return "Tap Continue. Let's play!";
                case "Point at this worker and press {0}.":case "First, select this worker with {0}.":return "Tap this worker to select it.";
                case "Select your headquarters with {0}.":case "First, select your headquarters with {0}.":return "Tap your headquarters to select it.";
                case "Select this military building with {0}.":return "Tap this military building to select it.";
                case "Point at the violet marker and press {0}.":case "Point at the violet outline and press {0}.":case "Aim at the ground marker and press {0}.":return "Tap the violet marker on the battlefield.";
                case "Aim at the enemy marker and press {0}.":return "Tap the enemy marker on the battlefield.";
                case "Point at this gas deposit and press {0}.":return "Tap this gas deposit to send your worker.";
                case "Point at these minerals and press {0}.":return "Tap these minerals to send your worker.";
                case "Click {0} in the console.":return "Tap {0} in the actions.";
                case "Click {0} once in the console.":return "Tap {0} once in the actions.";
                case "Click {0} {1} times in the console.":return "Tap {0} {1} times in the actions.";
                case "Click Attack Move in the console.":return "Tap Attack Move in the actions.";
                case "Press {0}, or choose Pause in the console.":return "Tap the menu button at the top of the screen.";
                case "Choose Resume match, or press {0}.":return "Tap Resume match.";
                case "Hold {0} and drag a box around your three soldiers.":return "Draw a circle around your three soldiers, then release.";
                case "Hold {0} and press {1} to save group 1.":
                    tutorialCueAction=mobileReplacingGroup?"group:0":"mobile-group";
                    return mobileReplacingGroup?"Tap group 1 to replace it with your selected soldiers.":"Tap Group selection to save your squad as group 1.";
                case "Press {0} to select your saved squad.":return "Tap group 1 to select your saved squad.";
                case "Press {0} to cancel, then select your worker.":return "Tap Cancel / clear, then tap your worker.";
                case "Press {0} to cancel, then select your squad.":return "Tap Cancel / clear, then select your squad.";
                default:return key;
            }
        }
    }
}
#endif
