using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel : MonoBehaviour
    {
        public GameSession Session;
        public BoardView Board;
        public QuestBridge Quest;
        public Action PlaceRequested;
        public HotkeyBindings Bindings { get; private set; }
        private Canvas canvas;
        private RectTransform root, actions;
        private Text headline, economy, selection, harvesting, status;
        private RectTransform statusBanner, statusAccent;
        private Text statusIcon;
        private static readonly Color ErrorTint=new Color(1,.38f,.36f,1);
        private Font font;
        private readonly List<Target> targets = new List<Target>();
        private string signature;
        public enum MenuPage { Main, MatchSetup, Settings, Hotkeys, Actions, Pause, LeaveConfirmation, Result, Guide, Guides, UnitsGuide, StructuresGuide, History, HistoryReport, Replay, OnlineSetup, OnlineEndpoint, DirectSetup, PublicEndpoint, Ranking, OnlineConnection }
        public MenuPage Page { get; private set; } = MenuPage.Main;
        public bool AcceptsGameInput => Page==MenuPage.Actions&&Session.CanIssueOrders;
        public bool ShowsBoard => Session.Running||Page==MenuPage.MatchSetup||Page==MenuPage.OnlineSetup||Page==MenuPage.DirectSetup||Quest.Placing;
        public bool IsDragging => moving;
        private readonly Stack<MenuPage> menuHistory=new Stack<MenuPage>();
        private int actionIndex;
        private Target pressed, hovered;
        private float pressedAt;
        private float nextText;
        private string lastMenuMessage;
        private uint lastMenuMessageVersion;
        private float menuMessageUntil;
        private RectTransform moveHandle, resetHandle;
        private Image moveImage, resetImage;
        private bool moving, positionInitialized;
        private Vector3 windowPosition, preferredWindowOffset=new Vector3(0,-.12f,1.25f);
        private float windowScale;
        private const float WindowRecallDistance=3.25f;
        private float grabDistance;
        private Vector3 grabOffset;
        private const string PlacementKey="frontier.panel-placement.v1";
        [Serializable] private sealed class PanelPlacement { public int version; public Vector3 offset; }
        private sealed class Target { public RectTransform rect; public Image image; public Color normal; public Action click, hold; public SelectionAction help; public string selectionType; public int selectionEntity; public string actionId; public Text detail; public string hintTitle; public Func<string> hint; public Dictionary<Graphic,Color> hoverInk; public Graphic hoverExtra; }
        private readonly Color background = ConsoleTheme.Background, button = ConsoleTheme.Card, accent = ConsoleTheme.Accent;
        private void Start()
        {
            Bindings=HotkeyBindings.Load();
            Quest.Bindings=Bindings;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if FRONTIER_BROWSER
            var shell=new GameObject("Browser interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));shell.transform.SetParent(transform,false);
            canvas=shell.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10;
            var scaler=shell.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var go=new GameObject("Command panel",typeof(RectTransform));go.transform.SetParent(shell.transform,false);
#else
            var go=new GameObject("Command console",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=10;
#endif
            root=go.GetComponent<RectTransform>();root.pivot=Vector2.one*.5f;root.sizeDelta=new Vector2(1100,526);
            baseWindowScale=Mathf.Clamp(Board.Width/1300f,.00065f,.0012f)*HudWorldScale;
            LoadWindowScales();ApplyWindowScale();
            var image=go.AddComponent<Image>();image.color=background;ConsoleTheme.StyleSurface(image);
            moveHandle=Handle("Move menu",new Vector2(20,-12),new Vector2(810,44),"MOVE MENU · hold Trigger / A and drag",out moveImage);
            resetHandle=Handle("Reset menu",new Vector2(844,-12),new Vector2(236,44),"Reset position",out resetImage);
            headline=Label(root,"LITTLE LEGION",new Vector2(30,-84),new Vector2(1040,44),30,accent);
            economy=Label(root,"",new Vector2(30,-130),new Vector2(1040,36),22,Color.white);
            selection=Label(root,"",new Vector2(30,-169),new Vector2(1040,38),20,new Color(.65f,.76f,.82f));
            harvesting=Label(root,"",new Vector2(30,-207),new Vector2(1040,28),18,accent);
            statusBanner=Section(root,"Status message",new Vector2(30,-473),new Vector2(1040,48),Color.clear);
            statusAccent=Section(statusBanner,"Error accent",Vector2.zero,new Vector2(5,48),ErrorTint);
            statusIcon=Label(statusBanner,"!",new Vector2(12,0),new Vector2(24,48),26,ErrorTint,TextAnchor.MiddleCenter);
            statusIcon.fontStyle=FontStyle.Bold;
            statusAccent.gameObject.SetActive(false);statusIcon.gameObject.SetActive(false);
            status=Label(statusBanner,"",Vector2.zero,new Vector2(1040,48),20,accent);
            actions=new GameObject("Actions",typeof(RectTransform)).GetComponent<RectTransform>();actions.SetParent(root,false);TopLeft(actions,new Vector2(30,-240),new Vector2(1040,225));
            CreateMatchHud();
            CreateTutorialCard();
            // Shared labels and action buttons sit above the HUD section backgrounds.
            foreach(var label in new[]{headline,economy,selection,harvesting})label.transform.SetAsLastSibling();
            statusBanner.SetAsLastSibling();
            actions.SetAsLastSibling();
            CreateActionHelp();
            CreateControlGuide();
            CreateHotkeys();
            CreateReferenceGuide();
            CreateConsoleChrome();
#if FRONTIER_BROWSER
            CreateBrowserHud();
#endif
            if(PlayerPrefs.HasKey(PlacementKey))
            {
                try
                {
                    var saved=JsonUtility.FromJson<PanelPlacement>(PlayerPrefs.GetString(PlacementKey));
                    // Version 1 was board-relative. It must not move the new shared window
                    // to an unrelated room location when the board anchor restores.
                    if(saved!=null&&saved.version==2&&IsFinite(saved.offset)&&saved.offset.magnitude<3.25f&&saved.offset.magnitude>.25f)
                        preferredWindowOffset=saved.offset;
                }
                catch(ArgumentException){PlayerPrefs.DeleteKey(PlacementKey);}
            }
            Session.NewMatch+=OnMatchStarted;Session.MatchClosed+=OnMatchClosed;
            LoadOnlineEndpoint();
            Page=Session.IsReplay?MenuPage.Replay:Session.Running?(Session.Winner!=-1&&!Session.IsTutorial?MenuPage.Result:MenuPage.Actions):MenuPage.Main;
            ApplyWindowScale();
            Canvas.willRenderCanvases+=LayoutHudCounters;
            Rebuild();
        }
        private void OnDestroy()
        {
            Canvas.willRenderCanvases-=LayoutHudCounters;
            DisposeTutorialPresentation();
            if(Session==null)return;
            Session.NewMatch-=OnMatchStarted;Session.MatchClosed-=OnMatchClosed;
        }
        private void OnMatchStarted(){shownReport=null;Show(Session.IsReplay?MenuPage.Replay:MenuPage.Actions);UpdateCountdownPresentation();}
        private void OnMatchClosed(){shownReport=null;if(Session.LastClosedWasReplay)OpenHistory();else Show(MenuPage.Main);}
        private void LateUpdate()
        {
            if(root==null)return;
            Board.SetVisible(ShowsBoard);
            if(!UpdatePosition(!Quest.Active||Quest.HeadTracked)){HideTutorialPresentation();return;}
            if(UpdateCountdownPresentation()){HideTutorialPresentation();return;}
            if(Session.IsConnecting&&!Session.Running)
            {var onlinePage=Session.IsPublicMatch?MenuPage.OnlineSetup:MenuPage.DirectSetup;if(Page!=onlinePage)Show(onlinePage);}
            if(Session.Running&&!Session.IsReplay&&!Session.IsTutorial&&Session.Winner!=-1&&Page!=MenuPage.Result)Show(MenuPage.Result);
            string state=Session.ArchiveFailed+"/"+Page+":"+Quest.Active+":"+Session.IsConnecting+":"+Session.IsMatchmaking+":"+(Session.ResultReport!=null)+":"+Session.OnlineReportUnavailable+":"+Session.BuildDefinition+":"+Session.RallyPointMode+":"+TutorialSignature+":"+string.Join(",",Session.View?.researched??Array.Empty<string>())+":"+string.Join(",",Session.Selection.OrderBy(x=>x))+":"+(Session.View==null?"":string.Join(",",Session.View.entities.Where(e=>Session.Selection.Contains(e.id)).Select(e=>e.definition+e.complete+":"+e.hasRallyPoint+":"+string.Join("/",e.queue.Select(q=>q.id)))));
#if FRONTIER_BROWSER
            state+=":"+BrowserMobileInput.IsTouchMode+":"+((RectTransform)canvas.transform).rect.size+":"+Screen.safeArea;
#endif
            if(Page==MenuPage.Ranking||Page==MenuPage.OnlineSetup||Page==MenuPage.Result)state+=":"+Session.RankingVersion;
            if(state!=signature){signature=state;Rebuild();}
            RefreshOrderTargets();
            if(!AcceptsGameInput||Quest.Placing){CloseSectionHelp();HideActionHelp();}
            if(SectionHelpOpen)UpdateActionHelp();
            UpdateGroupFeedback();
            UpdateTutorialPresentation();
            if(Time.unscaledTime<nextText)return;nextText=Time.unscaledTime+.15f;
            UpdateTutorialCard();
            if(Page!=MenuPage.Actions){UpdateMenuText();return;}
            UpdateMatchHud();
        }
        private bool UpdatePosition(bool headTracked)
        {
#if FRONTIER_BROWSER
            LayoutBrowserScreen();
            return true;
#else
            if(!headTracked||(Quest.Active&&!Session.Running&&Session.HasSystemPause))
            {
                if(moving){moving=false;SavePosition();}
                CancelPointerInteraction();root.gameObject.SetActive(false);return false;
            }
            var eye=Quest.Camera.transform;
            // Every page shares one center, with its own remembered scale.
            // Board placement and match transitions never select another anchor.
            if(positionInitialized&&!moving&&Vector3.Distance(windowPosition,eye.position)>WindowRecallDistance)
            {positionInitialized=false;preferredWindowOffset=new Vector3(0,-.12f,1.25f);CancelPointerInteraction();}
            if(!positionInitialized)
            {
                windowPosition=eye.position+WindowFacing()*preferredWindowOffset;
                positionInitialized=true;
            }
            root.gameObject.SetActive(true);root.localScale=Vector3.one*windowScale;
            if(!moving){root.position=windowPosition;FaceViewer();}
            return true;
#endif
        }
        private Quaternion WindowFacing()
        {
            var forward=Vector3.ProjectOnPlane(Quest.Camera.transform.forward,Vector3.up);
            if(forward.sqrMagnitude<.01f)forward=Vector3.Cross(Quest.Camera.transform.right,Vector3.up);
            return Quaternion.LookRotation(forward.normalized,Vector3.up);
        }
        private void UpdateHarvesting(VisibleEntity[] selected)
        {
            harvesting.text="";harvesting.color=accent;
            if(Session.View==null)return;
            var view=Session.View;
            int pointedResource=Session.SelectedResource>0&&Session.Selection.Count==0?Session.SelectedResource:
                Board.HoveredResource>0?Board.HoveredResource:Board.Hovered==0&&Board.Pointer.HasValue?Board.ResourceAt(Board.Pointer.Value):0;
            var resource=view.resources.FirstOrDefault(r=>r.id==pointedResource);
            var building=view.entities.FirstOrDefault(e=>e.id==Board.Hovered&&e.owner==view.player);
            if(resource==null&&building==null)
            {
                resource=view.resources.FirstOrDefault(r=>r.id==Session.SelectedResource);
                if(selected?.Length==1)building=selected[0];
            }
            if(resource==null&&building!=null&&Session.Catalog[building.definition].role=="extractor")
            {
                int size=Session.Catalog[building.definition].footprint;
                resource=view.resources.FirstOrDefault(r=>r.kind=="catalyst"&&r.position.x>=building.position.x&&r.position.x<building.position.x+size
                    &&r.position.y>=building.position.y&&r.position.y<building.position.y+size);
            }
            if(resource!=null)
            {
                string name=resource.kind=="mineral"?"MINERALS":"GAS / CATALYST";
                string remaining=resource.remaining<0?"?":resource.remaining.ToString();
                int optimum=resource.remaining==0?0:Harvesting.OptimalWorkers(resource.kind);
                harvesting.text=$"{name} · {Saturation(resource.assignedWorkers,optimum)} · Remaining: {remaining}";
                if(resource.kind=="catalyst"&&resource.extractorId==0&&resource.remaining!=0)((LocalizedText)harvesting).Append(" · Requires a completed extractor");
            }
            else if(building!=null&&Session.Catalog[building.definition].role=="hq")
            {
                var minerals=view.resources.Where(r=>r.baseId==building.id&&r.kind=="mineral").ToArray();
                var gas=view.resources.Where(r=>r.baseId==building.id&&r.kind=="catalyst").ToArray();
                harvesting.text="MINERALS · "+Saturation(minerals.Sum(r=>r.assignedWorkers),minerals.Sum(r=>r.optimalWorkers))+
                    "     GAS · "+Saturation(gas.Sum(r=>r.assignedWorkers),gas.Sum(r=>r.optimalWorkers));
            }
            else if(selected!=null&&selected.Any(e=>Session.Catalog[e.definition].role=="worker"))
                harvesting.text="Optimal: 2 workers per mineral patch · 3 per gas · Automatically assigned to nearby deposits";
        }
        private static string Saturation(int assigned,int optimal)
        {
            string value=$"Workers {assigned}/{optimal}"+(assigned>optimal?" (excess)":optimal>0&&assigned==optimal?" (optimal)":"");
            return assigned>optimal?"<color=#FF3830>"+value+"</color>":value;
        }
        private void UpdateMenuText()
        {
            if(lastMenuMessage!=Session.Message||lastMenuMessageVersion!=Session.MessageVersion)
            {lastMenuMessage=Session.Message;lastMenuMessageVersion=Session.MessageVersion;menuMessageUntil=Time.unscaledTime+8;}
            harvesting.text="";harvesting.color=accent;
            status.text=Session.Message;status.color=accent;
            switch(Page)
            {
                case MenuPage.Main:
                    headline.text="LITTLE LEGION";economy.text="Little armies. Big battles.";
                    selection.text="Lead your faction from a tabletop battlefield. Play offline or find an online opponent.";
#if FRONTIER_BROWSER
                    status.text="Choose New Match to begin.";break;
#else
                    status.text=Board.Placed?"Your saved play space is ready. Adjust it from New Match if needed.":"Open New Match to position your battlefield and start playing.";break;
#endif
                case MenuPage.MatchSetup:
                    headline.text="NEW MATCH";economy.text="SKIRMISH / LOCAL AI";
                    selection.text=$"{Session.Catalog.Faction(Session.Faction).name}  vs  {Session.Catalog.Faction(Session.EnemyFaction).name} · {DifficultyLabel} · {Session.Map.title}";
                    harvesting.text=Board.Placed?"BOARD READY":"BOARD NOT PLACED";
                    ((LocalizedText)harvesting).Append(Session.Difficulty=="hard"?" · Sustained attacks from 2:30.":Session.Difficulty=="easy"?" · Attacks from 5 min.":" · Attacks from 3:30.");
#if FRONTIER_BROWSER
                    status.text="Both factions start equally. Choose your army, opponent and map, then start the match.";break;
#else
                    status.text=Board.Placed?"Both factions start equally. Position board lets you adjust your play space.":"Choose Position board to place your battlefield, then Start Match when ready.";break;
#endif
                case MenuPage.OnlineSetup:case MenuPage.DirectSetup:case MenuPage.OnlineEndpoint:case MenuPage.PublicEndpoint:case MenuPage.OnlineConnection:
                    UpdateOnlineHeader();break;
                case MenuPage.Ranking:UpdateRankingHeader();break;
                case MenuPage.Settings:
                    headline.text="SETTINGS";economy.text=MenuOrigin+" / SETTINGS";
#if FRONTIER_BROWSER
                    selection.text=settingsTab==SettingsTab.Controls?"Mouse / keyboard · Camera and gameplay controls.":settingsTab==SettingsTab.Audio?"Set the balance between music and battlefield sounds.":settingsTab==SettingsTab.Visuals?"Adjust the battlefield's visual effects.":"Choose your interface language. System follows this device.";
#else
                    selection.text=settingsTab==SettingsTab.Controls?Bindings.HandName+" controller active. The "+Bindings.OtherHandName.ToLowerInvariant()+" controller is unused.":settingsTab==SettingsTab.Audio?"Set the balance between music and battlefield sounds.":settingsTab==SettingsTab.Visuals?"Adjust the battlefield's visual effects.":settingsTab==SettingsTab.Language?"Choose your interface language. System follows this device.":"Prepare your battlefield and choose where it sits in your room.";
#endif
                    status.text=settingsTab==SettingsTab.PlaySpace?(Board.Placed?"Board position saved. Reposition it whenever you change play space.":"No board position set. Placement will open before the match starts."):"Preferences are shared before and during a match, and saved on this device.";break;
                case MenuPage.Hotkeys:
                    headline.text="HOTKEYS";economy.text=hotkeysTouch?"QUEST · "+Bindings.HandName.ToUpperInvariant()+" TOUCH CONTROLLER":"DESKTOP · MOUSE / KEYBOARD";
                    selection.text=hotkeysTouch?"Active hand only. Menus: Trigger / "+Bindings.MenuConfirmName+" · "+Bindings.MenuBackName+" to go back.":"Edit gameplay inputs. Menus always use left-click and Escape to go back.";
                    status.text=hotkeyMessage;break;
                case MenuPage.Pause:
                    headline.text=Session.IsOnline?"MATCH MENU":"MATCH PAUSED";economy.text=Session.Map.title;
                    selection.text=MatchDescription;
                    status.text=Session.IsOnline?"The match continues while you browse menus. Return to the battlefield when ready.":"Take your time. The match stays paused while you browse guides and settings.";break;
                case MenuPage.Guides:
                    headline.text="GUIDES";economy.text=MenuOrigin+" / GUIDES";
                    selection.text="Learn the controls, compare units and plan your base.";
                    status.text="Unit and structure data show base values, before match upgrades.";break;
                case MenuPage.UnitsGuide:case MenuPage.StructuresGuide:
                    headline.text=Page==MenuPage.UnitsGuide?"UNIT GUIDE":"STRUCTURE GUIDE";economy.text="GUIDES / "+(Page==MenuPage.UnitsGuide?"UNITS":"STRUCTURES");
                    selection.text="Choose a faction and an entry to inspect its role and base stats.";
                    status.text="Base values before upgrades. Browsing does not change your match setup.";break;
                case MenuPage.Guide:
                    headline.text="CONTROL GUIDE";economy.text="COMMANDS / GESTURES";
                    selection.text="Choose a category to review its controls.";
                    status.text=Session.Running?(Session.IsOnline?"The online match continues. Back returns to Guides.":"Your match stays paused. Back returns to Guides."):"Review controls before playing. Back returns to Guides.";break;
                case MenuPage.LeaveConfirmation:
                    headline.text="LEAVE MATCH?";economy.text="RETURN TO MAIN MENU";
                    selection.text=Session.IsTutorial?"Leave this guided practice and return to the main menu? You can restart the tutorial there.":
                        Session.IsOnline?"Leaving concedes this match to your opponent.":"Leave this match and return to the main menu? It will be recorded as abandoned.";
                    status.text="Choose Keep playing to return to the pause menu.";break;
                case MenuPage.Result:
                    headline.text=Session.Winner==Session.LocalPlayer?"VICTORY":Session.Winner==2?"DRAW":"DEFEAT";
                    economy.text="MATCH COMPLETE · "+Session.Map.title+" · "+ReportTime(Session.Tick);
                    selection.text=MatchDescription;
                    harvesting.text="POST-MATCH REPORT";
                    status.text=Session.IsOnline?(Session.ResultReport==null?(Session.OnlineReportUnavailable?"The final report could not be received. The match result is preserved.":"Receiving the final report…"):"Final report received from the server. You can now find another match."):(Session.ArchiveStatus??"Both sides revealed after the match. Rematch starts a fresh report.");
                    if(Session.IsPublicMatch)UpdateMatchRatingHeader();break;
                case MenuPage.History:
                    headline.text="MATCH HISTORY";economy.text="SAVED ON THIS DEVICE";
                    selection.text="Watch a replay, or choose a match for statistics, graphs and build order.";
                    harvesting.text=savedMatches.Length+" recorded matches";
                    status.text=historyMessage??"Your matches stay available after closing the app.";break;
                case MenuPage.Replay:UpdateReplayHeader();break;
                case MenuPage.HistoryReport:
                    var saved=archivedReport.Info;var h=saved.Header;
                    headline.text=HistoryOutcome(saved).ToUpperInvariant();economy.text=h.mapTitle+" · "+ReportTime(archivedReport.Report.Tick)+" · "+HistoryDate(h.startedUtc);
                    selection.text=$"{ShortFaction(h.faction)} vs {ShortFaction(h.enemyFaction)} · {h.difficulty} AI";
                    harvesting.text="SAVED MATCH REPORT";status.text="Browsing this report does not start or change a match.";break;
            }
            if(Quest.Placing||Quest.StartPending||((Page==MenuPage.Main||Page==MenuPage.Settings||Page==MenuPage.MatchSetup||Page==MenuPage.History||Page==MenuPage.HistoryReport)&&Time.unscaledTime<menuMessageUntil))status.text=Session.Message;
            UpdateStatusStyle(Session.MessageIsError&&((LocalizedText)status).Source==Session.Message);
        }
        private void UpdateStatusStyle(bool error)
        {
            error|=Page==MenuPage.Replay&&Session.Replay?.Error!=null;
            error|=Page==MenuPage.Ranking&&Session.RankingFailed||Page==MenuPage.Result&&Session.IsPublicMatch&&Session.MatchRatingFailed;
            statusBanner.GetComponent<Image>().color=Color.clear;
            statusAccent.gameObject.SetActive(false);statusIcon.gameObject.SetActive(false);
            status.fontStyle=FontStyle.Normal;
            status.fontSize=Page==MenuPage.Replay?16:Page==MenuPage.Actions?18:20;
            status.alignment=Page==MenuPage.Main?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft;
            status.color=error?ErrorTint:ConsoleTheme.Muted;
            TopLeft(status.rectTransform,Vector2.zero,statusBanner.sizeDelta);
        }
        private void Add(string label,Action action,Action hold=null,bool primary=false)
        {
            bool grid=Page==MenuPage.Actions||Page==MenuPage.MatchSetup;
            int columns=Page==MenuPage.Main?1:grid?4:2;
            int n=actionIndex++,col=n%columns,row=n/columns;
            float width=columns==1?600:columns==2?505:246,height=grid?49:58;
            var go=new GameObject(label,typeof(RectTransform),typeof(Image));var rt=go.GetComponent<RectTransform>();rt.SetParent(actions,false);
            TopLeft(rt,new Vector2((columns==1?220:0)+col*(width+15),-row*(height+9)),new Vector2(width,height));
            var image=go.GetComponent<Image>();image.color=primary?ConsoleTheme.Primary:button;ConsoleTheme.StyleSurface(image);
            Label(rt,label,new Vector2(9,-4),new Vector2(width-18,height-8),grid?18:22,ConsoleTheme.Ink,TextAnchor.MiddleCenter);
            targets.Add(new Target{rect=rt,image=image,normal=image.color,click=action,hold=hold});
        }
        private void Rebuild()
        {
            if(actions==null)return;
            RestoreCountdownPresentation();
            ResetGroupGesture();
            CancelReplayScrub();
            // Hide the old controls immediately, including between pointer events in the same frame.
            for(int i=actions.childCount-1;i>=0;i--)
            {var child=actions.GetChild(i).gameObject;child.SetActive(false);if(Application.isPlaying)Destroy(child);else DestroyImmediate(child);}
            ApplyTargetHover(hovered,false);
            targets.Clear();pressed=null;hovered=null;actionIndex=0;nextText=0;
            ClearProductionTargets();ClearOrderTargets();
            HideActionHelp();
            moveHandle.GetComponentInChildren<Text>(true).text=Quest.Active?"MOVE MENU · hold Trigger / "+Bindings.MenuConfirmName+" and drag":"MOVE MENU · hold left mouse and drag";
            LayoutPage();
#if FRONTIER_BROWSER
            if(BrowserMobileInput.IsTouchMode&&BuildMobileMenu())return;
#endif
            BuildControlGuide();
            BuildHotkeys();
            BuildReferenceGuide();
            BuildTutorialCard();
            if(BuildMenuPage())return;
            if(Page==MenuPage.Hotkeys)
            {
                Add("Back",Back);
                if(!HotkeyModalOpen)Add("Restore device defaults",RequestHotkeyReset);
                return;
            }
            if(Page==MenuPage.Guide||IsReferencePage)
            {
                Add("Back",Back);
                if(Page==MenuPage.Guide&&controlGuidePages>1)Add($"Page {controlGuidePage+1} / {controlGuidePages} · Next",()=>{controlGuidePage=(controlGuidePage+1)%controlGuidePages;Rebuild();});
                return;
            }
            BuildMatchActions();
        }
        private void Show(MenuPage page)
        {
            // Keep the resume lesson beside its actual menu until the player
            // acknowledges the explanation. Back and Space share this route.
            if(page==MenuPage.Actions&&TutorialResumeBriefing)
            {Session.Announce("Choose Continue in the tutorial card before resuming.");return;}
            if(moving){moving=false;SavePosition();}
            CloseSectionHelp();
            if(page==MenuPage.Main||page==MenuPage.Actions||page==MenuPage.Pause||page==MenuPage.MatchSetup||page==MenuPage.OnlineSetup||page==MenuPage.DirectSetup||page==MenuPage.Result||page==MenuPage.Replay)menuHistory.Clear();
            if(page==MenuPage.Settings&&Page!=MenuPage.Hotkeys)settingsTab=SettingsTab.Controls;
            if(page==MenuPage.Hotkeys&&Page!=MenuPage.Hotkeys)
            {hotkeysTouch=Quest.Active;hotkeysPage=0;hotkeysEditing=true;hotkeyMessage="Choose an input to change. Changes are saved on this device.";}
            if(page!=MenuPage.Hotkeys)DismissHotkeyModal();
            if(page==MenuPage.Guide&&Page!=MenuPage.Guide)controlGuidePage=0;
            if(page!=MenuPage.MatchSetup&&page!=MenuPage.OnlineSetup&&page!=MenuPage.DirectSetup&&page!=MenuPage.Result)Quest.CancelStartRequest();
            Page=page;signature=null;moving=false;
            if(page==MenuPage.Pause||page==MenuPage.Actions)Session.NotifyTutorialMenu(page==MenuPage.Pause);
            ApplyWindowScale();
            lastMenuMessage=Session.Message;lastMenuMessageVersion=Session.MessageVersion;menuMessageUntil=0;
            ClearSelectionGesture();
            // Only this menu's pause is released. Tracking and headset pauses stay authoritative.
            Session.Pause("menu",Session.Running&&page!=MenuPage.Actions&&page!=MenuPage.Result&&page!=MenuPage.Replay);
            Board.Hovered=0;Board.Pointer=null;Board.DrawSelection(default,default,false);
            Board.SetVisible(ShowsBoard);Rebuild();
            if(Application.isPlaying&&!Session.IsConnecting&&(page==MenuPage.OnlineSetup||page==MenuPage.Ranking))_=Session.RefreshRankingAsync();
        }
        private void OpenSubmenu(MenuPage page){menuHistory.Push(Page);Show(page);}
        private void ToggleControllerHand()
        {
            Bindings.SelectHand(!Bindings.LeftHand);Bindings.Save();CancelPointerInteraction();
            Rebuild();UpdateMenuText();
        }
        public void Back()
        {
            if(Session.IsCountingDown&&!Quest.Placing)return;
            if(SectionHelpOpen){CloseSectionHelp();return;}
            if(HotkeyModalOpen){CancelHotkeyChange();return;}
            if(Quest.Placing){Quest.CancelPlacement();return;}
            switch(Page)
            {
                case MenuPage.MatchSetup:Show(MenuPage.Main);break;
                case MenuPage.OnlineSetup:CancelOnlineSetup();break;
                case MenuPage.DirectSetup:Quest.CancelStartRequest();Session.CancelOnline();Show(MenuPage.OnlineSetup);break;
                case MenuPage.OnlineEndpoint:Show(MenuPage.DirectSetup);break;
                case MenuPage.PublicEndpoint:Show(MenuPage.OnlineConnection);break;
                case MenuPage.OnlineConnection:case MenuPage.Ranking:Show(MenuPage.OnlineSetup);break;
                case MenuPage.Settings:case MenuPage.Hotkeys:case MenuPage.Guides:case MenuPage.Guide:case MenuPage.UnitsGuide:case MenuPage.StructuresGuide:
                    Show(menuHistory.Count>0?menuHistory.Pop():Page==MenuPage.Hotkeys?MenuPage.Settings:Page==MenuPage.Guide||IsReferencePage?MenuPage.Guides:Session.Running?MenuPage.Pause:MenuPage.Main);break;
                case MenuPage.Actions:Show(MenuPage.Pause);break;
                case MenuPage.Pause:Show(MenuPage.Actions);break;
                case MenuPage.LeaveConfirmation:Show(MenuPage.Pause);break;
                case MenuPage.Result:Session.LeaveMatch();break;
                case MenuPage.History:archivedReport=null;shownReport=null;Show(MenuPage.Main);break;
                case MenuPage.HistoryReport:Show(MenuPage.History);break;
                case MenuPage.Replay:Session.StopReplay();break;
            }
        }
        public void ToggleMenu()
        {
            if(Session.IsCountingDown)return;
            if(SectionHelpOpen){CloseSectionHelp();return;}
            if(Session.IsReplay){Session.ToggleReplayPause();return;}
            if(HotkeyModalOpen){CancelHotkeyChange();return;}
            if(Page==MenuPage.History||Page==MenuPage.HistoryReport||Page==MenuPage.Settings||Page==MenuPage.Hotkeys||Page==MenuPage.Guides||Page==MenuPage.Guide||IsReferencePage){Back();return;}
            if(Quest.Placing){Quest.CancelPlacement();return;}
            if(!Session.Running){Back();return;}
            if(Session.Winner!=-1)return;
            Show(Page==MenuPage.Actions?MenuPage.Pause:MenuPage.Actions);
        }
        public void ResetPosition(bool forgetSaved=true)
        {
            if(forgetSaved)
            {
                preferredWindowOffset=new Vector3(0,-.12f,1.25f);
                PlayerPrefs.DeleteKey(PlacementKey);PlayerPrefs.Save();
            }
            positionInitialized=false;
        }
        // A shortcut can still recover the shared window if it is out of view.
        public void Recall(){if(Session.IsCountingDown)return;CancelPointerInteraction();ResetPosition();Session.Announce("Menu reset in front of you.");}
        public void CancelPointerInteraction(bool closeHelp=true)
        {
            if(moving){moving=false;SavePosition();}
            ResetGroupGesture();
            if(closeHelp)CloseSectionHelp();
            CancelReplayScrub();
            ClearSelectionGesture();
            pressed=null;
            ApplyTargetHover(hovered,false);
            hovered=null;
            UpdateProductionHover();
            HideActionHelp();
            UpdateMoveAffordance(false);
        }
        public bool ContainsPointer(Ray ray)=>SectionHelpOpen||moving||
#if FRONTIER_BROWSER
            BrowserContainsPointer(ray);
#else
            Hit(root,ray,out _)||Hit(moveHandle,ray,out _)||Hit(tutorialCard,ray,out _);
#endif
        private void FaceViewer()
        {
            var direction=root.position-Quest.Camera.transform.position;
            if(direction.sqrMagnitude<.001f)return;
            float pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
            root.rotation=Quaternion.Euler(Mathf.Clamp(pitch,-25,25),Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,0);
        }
        private void SavePosition()
        {
            windowPosition=root.position;positionInitialized=true;
            preferredWindowOffset=Quaternion.Inverse(WindowFacing())*(windowPosition-Quest.Camera.transform.position);
            SaveWindowScale();
            PlayerPrefs.SetString(PlacementKey,JsonUtility.ToJson(new PanelPlacement{version=2,offset=preferredWindowOffset}));PlayerPrefs.Save();
        }
        private void OnApplicationFocus(bool focused)
        {
            if(root==null)return;
            if(focused)return;
#if FRONTIER_BROWSER
            CancelMobileGroupReplacement();
#endif
            if(HotkeyModalOpen)CancelHotkeyChange();
            if(moving){moving=false;SavePosition();}
            CancelPointerInteraction();
        }
        private void OnApplicationPause(bool paused)=>OnApplicationFocus(!paused);
        private static bool IsFinite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
        private static string Other(string faction)=>faction=="industrial"?"biological":"industrial";
        private static string ShortFaction(string faction)=>faction=="industrial"?"Consortium":"Symbiosis";
        private string DifficultyLabel=>Session.Difficulty=="hard"?"Hard":Session.Difficulty=="easy"?"Easy":"Normal";
        private string QueueLabel(string id)=>Session.Catalog.Has(id)?Session.Catalog[id].name:Session.Catalog.Upgrade(id)?.name??id;
        public bool Point(Ray ray,bool down,bool up,float depth=0,bool assignGroup=false,float scale=0)
        {
            if(Session.IsCountingDown&&!Quest.Placing){CancelPointerInteraction();return true;}
            if(root==null||!root.gameObject.activeInHierarchy)return false;
            if(SectionHelpOpen)return PointSectionHelp(ray,down,up);
#if FRONTIER_BROWSER
            if(Page==MenuPage.Actions&&!Hit(tutorialCard,ray,out _)&&browserMinimap!=null&&BrowserMobileInput.HasPointer
                &&browserMinimap.Point(BrowserMobileInput.PointerPosition,down,!up&&BrowserMobileInput.PointerHeld))
            {CancelPointerInteraction();return true;}
#endif
            if(moving)
            {
                UpdateMoveAffordance(true);
                AdjustWindowScale(scale,Time.unscaledDeltaTime);
                grabDistance=Mathf.Clamp(grabDistance+depth*Time.unscaledDeltaTime,.4f,3);
                var position=ray.GetPoint(grabDistance)+grabOffset;
                position.y=Mathf.Clamp(position.y,.25f,2.5f);
                var origin=Quest.Camera.transform.position;
                root.position=origin+Vector3.ClampMagnitude(position-origin,3);
                if(up){moving=false;SavePosition();}
                return true;
            }
            bool onTutorial=Hit(tutorialCard,ray,out _);
            bool onMove=Hit(moveHandle,ray,out float distance)&&!onTutorial;
            bool onReset=Hit(resetHandle,ray,out _)&&!onTutorial;
            UpdateMoveAffordance(onMove);
            resetImage.color=onReset?ConsoleTheme.Primary:background;
            if(down&&onMove)
            {
                CancelPointerInteraction();moving=true;grabDistance=distance;grabOffset=root.position-ray.GetPoint(distance);
                UpdateMoveAffordance(true);
                return true;
            }
            if(down&&onReset){Recall();return true;}
            if(PointReplayTimeline(ray,down,up))return true;
            Target hit=null;
            foreach(var target in targets)
            {
                // The guide can now sit over the console. Its solid background
                // must consume the ray instead of activating a covered command.
                if(onTutorial&&!target.rect.IsChildOf(tutorialCard))continue;
                if(target.rect.gameObject.activeInHierarchy&&Hit(target.rect,ray,out _)){hit=target;break;}
            }
            if(hovered!=hit){ApplyTargetHover(hovered,false);hovered=hit;HideActionHelp();ApplyTargetHover(hovered,true);}
            UpdateProductionHover();
            if(PointSelectionGrid(hit,down,up))return true;
            if(down){HideActionHelp();if(hit!=null){pressed=hit;pressedAt=Time.unscaledTime;BeginGroupGesture(hit,assignGroup);}}
            if(HandleGroupGesture(hit,up))return true;
            UpdateActionHelp();
            if(up&&pressed!=null)
            {var p=pressed;pressed=null;HideActionHelp();if(hit==p){Session.GetComponent<AudioFeedback>()?.Navigation(true);if(Time.unscaledTime-pressedAt>.65f&&p.hold!=null)p.hold();else p.click?.Invoke();}return true;}
            // Empty areas also consume the ray, preventing orders through the panel.
#if FRONTIER_BROWSER
            return pressed!=null||onMove||ContainsPointer(ray);
#else
            return pressed!=null||onMove||Hit(root,ray,out _)||Hit(tutorialCard,ray,out _);
#endif
        }
        private static bool Hit(RectTransform rect,Ray ray,out float distance)
        {
            distance=0;if(rect==null||!rect.gameObject.activeInHierarchy)return false;
#if FRONTIER_BROWSER
            return BrowserMobileInput.HasPointer&&RectTransformUtility.RectangleContainsScreenPoint(rect,BrowserMobileInput.PointerPosition,null);
#else
            if(!new Plane(rect.forward,rect.position).Raycast(ray,out distance)||distance>8)return false;
            var local=rect.InverseTransformPoint(ray.GetPoint(distance));return rect.rect.Contains(new Vector2(local.x,local.y));
#endif
        }
        private RectTransform Handle(string name,Vector2 position,Vector2 size,string label,out Image image)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rect=go.GetComponent<RectTransform>();rect.SetParent(root,false);TopLeft(rect,position,size);
            image=go.GetComponent<Image>();image.color=background;ConsoleTheme.StyleSurface(image);
            Label(rect,label,new Vector2(8,0),size-new Vector2(16,0),18,accent,TextAnchor.MiddleCenter);return rect;
        }
        private Text Label(RectTransform parent,string value,Vector2 position,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {var go=new GameObject("Label",typeof(RectTransform),typeof(LocalizedText));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);TopLeft(rect,position,size);var text=go.GetComponent<Text>();text.font=font;text.fontSize=fontSize;text.color=color;text.text=value;text.alignment=alignment;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.raycastTarget=false;return text;}
        private static void TopLeft(RectTransform rect,Vector2 position,Vector2 size){rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;}
    }
}
