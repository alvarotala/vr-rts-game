using Frontier.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if FRONTIER_BROWSER
using OVRInput = Frontier.Runtime.BrowserTouchInput;
#endif

namespace Frontier.Runtime
{
    public sealed partial class CommanderInput:MonoBehaviour
    {
        public GameSession Session;public BoardView Board;public CommandPanel Panel;public QuestBridge Quest;public ActionWheel Wheel;
        private Point start;private bool dragging, startedOnMap;private LineRenderer laser;private float hapticEnd;
        private ControllerVisuals controllerVisuals;
        private OVRInput.Controller hapticController=OVRInput.Controller.None;
        private bool wheelReleasePending;
        private OVRInput.Controller inputController=OVRInput.Controller.None;
        private bool controllerReleasePending;
        private bool countdownReleasePending;
        private enum PanelButton { None, Trigger, Action }
        private PanelButton panelButton;
        private bool panelReleasePending;
        private bool wheelTriggerHeld;
        private int pressedResource;
        private int pressedEntity;
        private float selectPressedAt;
        private bool deselectHoldCandidate,deselectHoldConsumed,areaGesture;
        private const float DeselectHoldTime=.5f;
        private float dragThreshold=1.5f;
        private const float DoubleTapWindow=.35f;
        public const float AttackDoubleTapWindow=.55f;
        private const float AttackTapAimTolerance=.035f;
        private float lastActionPress=-1,lastSelectPress=-1;
        private int actionSequence,actionEntity,actionResource,selectEntity;
        private Point actionPoint;
        private bool actionAdd,selectAdd,actionConfirmedTarget;
        private object gestureMatch;
        private object replaySource;
        private bool replayStickReady,replayMiddleDrag;
        private bool placementInputActive,placementInputReady;
        private int placementHeightDirection;
        private float placementHeightHeldSince,placementHeightUpdatedAt;
        private readonly HashSet<int> actionSelection=new HashSet<int>(),selectSelection=new HashSet<int>();
        private void Start()
        {
            Panel.PlaceRequested=Quest.BeginPlacement;Panel.TutorialInput=this;Session.Feedback+=Haptic;
#if !FRONTIER_BROWSER
            controllerVisuals=new ControllerVisuals(transform);
#endif
            var go=new GameObject("Pointer ray");laser=go.AddComponent<LineRenderer>();laser.sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));laser.sharedMaterial.color=new Color(.3f,.95f,.94f);laser.positionCount=2;laser.widthMultiplier=.002f;laser.enabled=Quest.Active;
        }
        private void LateUpdate()
        {
            // Read after input routing so pointer mode changes appear in this frame.
            if(laser!=null&&Board!=null)laser.sharedMaterial.color=Board.PointerColor;
            controllerVisuals?.Update(Quest,Board.PointerColor);
        }
        private void Update()
        {
            UpdateHaptics();
            if(Board.Content==null)
            {
#if FRONTIER_BROWSER
                mobileGesture.CancelUntilRelease();ClearMobileLasso();
#endif
                ResetMapGestures();Wheel?.TickHint(default,false);return;
            }
#if FRONTIER_BROWSER
            if(!Quest.Active&&BrowserMobileInput.IsTouchMode){RouteMobileInput();return;}
#endif
            bool down=false,up=false,order=false,add=false,cancel=false,toggleMenu=false;Vector2 pan=Vector2.zero;float zoom=0,menuDepth=0;Ray ray;
            Vector2 primaryStick=Vector2.zero;bool wheelPressed=false,wheelHeld=false,wheelReleased=false,triggerHeld=false,gripDown=false;
            bool gameplay=Panel.AcceptsGameInput&&!Quest.Placing;
            var bindings=Panel.Bindings;
            if(Quest.Active)
            {
                // Buttons, axes and the pointer always come from the chosen hand.
                var primary=Quest.ActiveController;
                if(inputController!=primary)ChangeInputController(primary);
                if(!ValidatePointerTracking(Quest.PointerTracked))return;
                ray=Quest.PointerRay();
                var select=HotkeyInput.Read(gameplay?bindings.Get(true,"select"):"touch:trigger",primary);
                var action=HotkeyInput.Read(gameplay?bindings.Get(true,"order"):"touch:a",primary);
                var modifier=Session.IsReplay?HotkeyInput.Read("touch:grip",primary):HotkeyInput.Read(bindings,true,"add");
                // Groups use physical stick click, just as wheel navigation uses
                // physical Trigger/B. Outside Groups the hint hotkey remains editable.
                var stickClick=HotkeyInput.Read("touch:stick",primary);
                var wheel=Session.IsReplay||Wheel!=null&&Wheel.InGroupMenu?stickClick:HotkeyInput.Read(bindings,true,"wheel");
                down=select.Down;up=select.Up;triggerHeld=select.Held;
                add=modifier.Held;gripDown=modifier.Down;order=action.Down;
                cancel=HotkeyInput.Read(gameplay?bindings.Get(true,"back"):"touch:b",primary).Down;
                primaryStick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,primary);
                wheelPressed=wheel.Down;wheelHeld=wheel.Held||stickClick.Held;wheelReleased=wheel.Up;
                menuDepth=primaryStick.y;
                laser.enabled=!Session.HasSystemPause&&Quest.PointerTracked;
                laser.SetPosition(0,ray.GetPoint(ControllerVisuals.EmitterDistance));laser.SetPosition(1,ray.GetPoint(2));
            }
            else
            {
                if(Mouse.current==null)return;ray=Quest.Camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                var select=HotkeyInput.Read(gameplay?bindings.Get(false,"select"):"mouse:left");
                down=select.Down;up=select.Up;triggerHeld=select.Held;order=gameplay&&HotkeyInput.Read(bindings,false,"order").Down;
                add=HotkeyInput.Read(bindings,false,"add").Held;
                cancel=HotkeyInput.Read(gameplay?bindings.Get(false,"back"):"key:Escape").Down;
                toggleMenu=(gameplay||Panel.Page==CommandPanel.MenuPage.Pause)&&HotkeyInput.Read(bindings,false,"pause").Down;
                zoom=Mouse.current.scroll.ReadValue().y*.04f;menuDepth=zoom;
                pan=new Vector2((HotkeyInput.Read(bindings,false,"pan-right").Held?1:0)-(HotkeyInput.Read(bindings,false,"pan-left").Held?1:0),
                    (HotkeyInput.Read(bindings,false,"pan-up").Held?1:0)-(HotkeyInput.Read(bindings,false,"pan-down").Held?1:0));
            }
            if((Session.IsCountingDown||countdownReleasePending)
                &&BlockCountdownInput(HotkeyInput.AnyHeld(Quest.Active,Quest.ActiveController)||primaryStick.magnitude>=.25f))return;
            if(placementInputActive&&!Quest.Placing)
            {placementInputActive=placementInputReady=false;placementHeightDirection=0;controllerReleasePending=Quest.Active;ResetWheelRouting();CancelPanelInput();ClearPointer();}
            Wheel?.TickHint(ray,Quest.Active&&!controllerReleasePending);
            if(Quest.Active&&controllerReleasePending)
            {
                controllerReleasePending=HotkeyInput.AnyHeld(true,Quest.ActiveController)||primaryStick.magnitude>=.25f;
                ClearPointer();return;
            }
            if(Panel.CaptureHotkeyInput(ray))
            {ResetWheelRouting();CancelPanelInput();ClearPointer();return;}
            ValidateMapGestures(add);
            if(Quest.Placing)
            {RoutePlacement(ray,primaryStick,Time.unscaledTime,Time.unscaledDeltaTime);return;}
            var menuSelect=HotkeyInput.Read(Quest.Active?"touch:trigger":"mouse:left",Quest.ActiveController);
            var menuAction=Quest.Active?HotkeyInput.Read("touch:a",Quest.ActiveController):default;
            // Section guides own all input while open, including rays outside the
            // console. Back dismisses help before it can cancel a targeting mode.
            if(Panel.SectionHelpOpen)
            {
                if(cancel||toggleMenu||HotkeyInput.Read(Quest.Active?"touch:b":"key:Escape",Quest.ActiveController).Down)
                {Panel.Back();CancelPanelInput();}
                else RoutePanel(ray,menuSelect.Down,menuSelect.Up,menuSelect.Held,menuAction.Down,menuAction.Up,menuAction.Held,0);
                ResetWheelRouting();ClearPointer();Wheel?.TickHint(ray,false);return;
            }
            bool uiPointerInput=menuSelect.Down||menuSelect.Up||menuSelect.Held||menuAction.Down||menuAction.Up||menuAction.Held||panelButton!=PanelButton.None||Panel.IsDragging;
            bool interactingWithPanel=panelButton!=PanelButton.None||Panel.IsDragging||(uiPointerInput&&Panel.ContainsPointer(ray));
            if(!Quest.Active&&Panel.Page!=CommandPanel.MenuPage.Hotkeys&&!interactingWithPanel&&!panelReleasePending)
            {
                if(HotkeyInput.Read(bindings,false,"menu").Down)
#if FRONTIER_BROWSER
                {Panel.ToggleMenu();CancelPanelInput();ClearPointer();return;}
#else
                    Panel.Recall();
#endif
                if(Panel.AcceptsGameInput||Panel.AcceptsReplayInput||Panel.Page==CommandPanel.MenuPage.MatchSetup)
                {
                    if(HotkeyInput.Read(bindings,false,"fit").Down)
#if FRONTIER_BROWSER
                        Quest.Navigation.Fit();
#else
                        Board.Fit();
#endif
                }
                if(Panel.AcceptsGameInput)
                {
                    bool save=HotkeyInput.Read(bindings,false,"save").Held;
                    for(int i=0;i<GameSession.ControlGroupCount;i++)if(HotkeyInput.Read(bindings,false,"group"+(i+1)).Down)Session.Group(i,save);
                }
            }
            // Replay transport is a separate route: recorded state never receives live orders,
            // group bindings or wheel navigation, including when playback is paused.
            if(Session.IsReplay)
            {
                RouteReplay(ray,primaryStick,menuSelect,menuAction,wheelPressed,zoom,menuDepth);
                return;
            }
            replaySource=null;replayStickReady=replayMiddleDrag=false;
            // Fixed console clicks keep priority over gameplay bindings and stick navigation.
            if((Wheel==null||!Wheel.IsOpen)&&uiPointerInput&&Panel.ContainsPointer(ray))
            {
                bool uiClick=menuSelect.Down||menuSelect.Held||menuAction.Down||menuAction.Held||panelButton!=PanelButton.None;
                if(HotkeyInput.Read(Quest.Active?"touch:b":"key:Escape",Quest.ActiveController).Down||(!uiClick&&(cancel||toggleMenu)))
                {
                    if(toggleMenu)Panel.ToggleMenu();
                    else if(Panel.AcceptsGameInput&&(Session.HasTargetingMode))Session.CancelMode();else Panel.Back();
                    CancelPanelInput();ClearPointer();return;
                }
                RoutePanel(ray,menuSelect.Down,menuSelect.Up,menuSelect.Held,menuAction.Down,menuAction.Up,menuAction.Held,menuDepth);
                ResetWheelRouting();ClearPointer();return;
            }
            bool wheelBack=Quest.Active&&Wheel!=null&&Wheel.IsOpen
                ?HotkeyInput.Read("touch:b",Quest.ActiveController).Down:cancel;
            if(Quest.Active&&RouteWheel(ray,primaryStick,wheelPressed,order||down||up||triggerHeld,menuSelect.Down,menuSelect.Held,wheelBack,add,gripDown,Time.unscaledTime,wheelHeld,wheelReleased))return;
            // Back/pause must work even when the pointer is over a menu button.
            if(cancel)
            {if(Panel.AcceptsGameInput&&(Session.HasTargetingMode))Session.CancelMode();else Panel.Back();CancelPanelInput();ClearPointer();return;}
            if(toggleMenu){Panel.ToggleMenu();CancelPanelInput();ClearPointer();return;}
#if FRONTIER_BROWSER
            // Hovering the screen HUD consumes map clicks, but keyboard camera
            // movement remains available. The camera itself gates wheel zoom.
            if(!Quest.Active&&(Panel.AcceptsGameInput||Panel.Page==CommandPanel.MenuPage.MatchSetup))
                Quest.Navigation.Navigate(pan,zoom,Time.unscaledDeltaTime);
#endif
            // Pointer UI uses stable controls even after gameplay selection is rebound.
            if(RoutePanel(ray,menuSelect.Down,menuSelect.Up,menuSelect.Held,menuAction.Down,menuAction.Up,menuAction.Held,menuDepth)){ClearPointer();return;}
            // Quest keeps the confirmed board view fixed. Navigation remains a desktop
            // development control; the active stick operates the wheel, placement or a grabbed menu.
#if !FRONTIER_BROWSER
            if(!Quest.Active&&(Panel.AcceptsGameInput||Panel.Page==CommandPanel.MenuPage.MatchSetup))
                Board.Navigate(pan,zoom,zoom!=0?1:Time.unscaledDeltaTime);
#endif
            if(!Panel.AcceptsGameInput){ClearPointer();return;}
            PointBoard(ray,down,up,order,add);
        }
        private bool BlockCountdownInput(bool held)
        {
            // Tracking recovery may require replacing the board; that system flow
            // pauses the offline count and never enables gameplay commands.
            if(Quest.Placing)return false;
            if(Session.IsCountingDown)countdownReleasePending=true;
            else if(!countdownReleasePending)return false;
            else if(!held)countdownReleasePending=false;
            ResetMapGestures();ResetWheelRouting();CancelPanelInput();ClearPointer();
            Wheel?.TickHint(default,false);
            if(laser!=null)laser.enabled=false;
            return true;
        }
        private void RoutePlacement(Ray ray,Vector2 stick,float now,float delta)
        {
            ResetWheelRouting();CancelPanelInput();ClearPointer();
            if(!placementInputActive){placementInputActive=true;placementInputReady=false;placementHeightDirection=0;}
            if(Session.HasSystemPause)
            {placementInputReady=false;placementHeightDirection=0;return;}
            bool touch=Quest.Active;var controller=Quest.ActiveController;
            // Drain the click which opened placement and any held controls from
            // an interruption. Placement navigation is fixed, like menu input.
            if(!placementInputReady)
            {
                placementInputReady=!HotkeyInput.AnyHeld(touch,controller)&&stick.magnitude<.25f;
                Wheel?.TickHint(ray,touch);return;
            }
            var confirm=HotkeyInput.Read(touch?"touch:a":"mouse:left",controller);
            if(HotkeyInput.Read(touch?"touch:b":"key:Escape",controller).Down)Quest.CancelPlacement();
            else
            {
                if(touch&&HotkeyInput.Read("touch:stick",controller).Down)Wheel?.ToggleHints();
                if(touch)
                {
                    float height=Mathf.Abs(stick.y)>.15f?stick.y:0;
                    float rotation=Mathf.Abs(stick.x)>.15f?stick.x:0;
                    float size=(HotkeyInput.Read("touch:trigger",controller).Held?1:0)-(HotkeyInput.Read("touch:grip",controller).Held?1:0);
                    Quest.PlaceRay(ray,height,new Vector2(rotation,size),confirm.Down,delta);
                }
                else
                {
                    var raise=HotkeyInput.Read("key:PageUp");var lower=HotkeyInput.Read("key:PageDown");
                    int direction=(raise.Held?1:0)-(lower.Held?1:0);
                    if(direction!=placementHeightDirection)
                    {
                        placementHeightDirection=direction;placementHeightHeldSince=placementHeightUpdatedAt=now;
                        if(direction!=0)Quest.AdjustPlacementHeight(direction*.025f);
                    }
                    else if(direction!=0)
                    {
                        float elapsed=Mathf.Clamp(now-Mathf.Max(placementHeightUpdatedAt,placementHeightHeldSince+.3f),0,.05f);
                        Quest.AdjustPlacementHeight(direction*elapsed*.3f);placementHeightUpdatedAt=now;
                    }
                    Quest.PlaceRay(ray,0,Vector2.zero,confirm.Down,delta);
                }
            }
            Wheel?.TickHint(ray,touch&&Quest.Placing);
            if(!Quest.Placing){placementInputActive=placementInputReady=false;placementHeightDirection=0;controllerReleasePending=touch;}
        }
        private void RouteReplay(Ray ray,Vector2 stick,HotkeyInput.Signal select,HotkeyInput.Signal action,bool hintDown,float scroll,float depth)
        {
            ResetMapGestures();wheelTriggerHeld=false;
            if(Wheel!=null&&Wheel.IsOpen)Wheel.Close();
            if(!ReferenceEquals(replaySource,Session.Replay))
            {replaySource=Session.Replay;replayStickReady=false;replayMiddleDrag=false;}
            if(!Panel.AcceptsReplayInput)
            {
                replayStickReady=replayMiddleDrag=false;CancelPanelInput();ClearPointer();Wheel?.TickHint(ray,false);return;
            }
            bool touch=Quest.Active;
            bool grip=touch&&HotkeyInput.Read("touch:grip",Quest.ActiveController).Held;
            bool back=HotkeyInput.Read(touch?"touch:b":"key:Escape",Quest.ActiveController).Down;
            bool play=touch?action.Down:HotkeyInput.Read("key:Space").Down;
            if(back&&!grip){Panel.Back();CancelPanelInput();ClearPointer();Wheel?.TickHint(ray,false);return;}
            if(!touch&&!Panel.IsDragging)
            {
                if(play)Session.ToggleReplayPause();play=false;
                if(HotkeyInput.Read("key:LeftArrow").Down)SeekReplayBy(-15);
                if(HotkeyInput.Read("key:RightArrow").Down)SeekReplayBy(15);
                if(HotkeyInput.Read("key:UpArrow").Down)ChangeReplaySpeed(1);
                if(HotkeyInput.Read("key:DownArrow").Down)ChangeReplaySpeed(-1);
            }
            bool uiEdge=select.Down||select.Up||select.Held||!grip&&(action.Down||action.Up||action.Held);
            bool panelActive=panelButton!=PanelButton.None||Panel.IsDragging||uiEdge&&Panel.ContainsPointer(ray);
            if(panelActive||panelReleasePending)
            {
                // A/X still confirms the menu under the laser. A gesture that began on
                // the console owns its release and cannot become transport or zoom.
                RoutePanel(ray,select.Down,select.Up,select.Held,!grip&&action.Down,action.Up,!grip&&action.Held,Panel.IsDragging?depth:0);
                replayStickReady=false;replayMiddleDrag=false;ClearPointer();Wheel?.TickHint(ray,touch&&!Panel.IsDragging);return;
            }
            RoutePanel(ray,false,false,false,false,false,false,0);
            Vector2 move=Vector2.zero;
            if(!touch)
            {
                move=new Vector2((HotkeyInput.Read("key:D").Held?1:0)-(HotkeyInput.Read("key:A").Held?1:0),
                    (HotkeyInput.Read("key:W").Held?1:0)-(HotkeyInput.Read("key:S").Held?1:0));
#if !FRONTIER_BROWSER
                var mouse=Mouse.current;
                if(mouse!=null)
                {
                    if(mouse.middleButton.wasPressedThisFrame)replayMiddleDrag=true;
                    if(!mouse.middleButton.isPressed)replayMiddleDrag=false;
                    if(replayMiddleDrag)
                    {var delta=mouse.delta.ReadValue();Board.Navigate(new Vector2(-delta.x,-delta.y),0,.006f);}
                }
#endif
            }
            ApplyReplayControls(stick,play,back,grip,hintDown,move,scroll,Time.unscaledDeltaTime,touch);
            if(Panel.ContainsPointer(ray))ClearPointer();else PointReplayBoard(ray);
            Wheel?.TickHint(ray,touch&&!controllerReleasePending);
        }
        // Explicit edges keep this testable without manufacturing OVR hardware frames.
        private void ApplyReplayControls(Vector2 stick,bool play,bool back,bool grip,bool hintDown,Vector2 move,float zoom,float delta,bool touch)
        {
            if(!Panel.AcceptsReplayInput)return;
            if(touch&&grip)
            {
                replayStickReady=false;
                Board.Navigate(stick*.65f,0,delta);
                if(play!=back&&(play||back))Board.Navigate(Vector2.zero,play ? .25f : -.25f,1);
            }
            else
            {
                if(play)Session.ToggleReplayPause();
                if(touch)
                {
                    if(stick.magnitude<.25f)replayStickReady=true;
                    else if(replayStickReady&&stick.magnitude>=.65f)
                    {
                        replayStickReady=false;
                        if(Mathf.Abs(stick.x)>Mathf.Abs(stick.y))SeekReplayBy(stick.x>0?15:-15);
                        else ChangeReplaySpeed(stick.y>0?1:-1);
                    }
                }
            }
            if(hintDown&&touch)Wheel?.ToggleHints();
            if(!touch)
#if FRONTIER_BROWSER
                Quest.Navigation.Navigate(move,zoom,delta);
#else
                Board.Navigate(move,zoom,zoom!=0?1:delta);
#endif
        }
        private void SeekReplayBy(int seconds)
        {
            var replay=Session.Replay;if(replay==null)return;
            int tick=replay.Seeking?replay.SeekTarget:replay.Tick;
            Session.SeekReplay(tick+seconds*Simulation.TickRate);PulseHaptic(.12f,.025f);
        }
        private void ChangeReplaySpeed(int direction)
        {
            var replay=Session.Replay;if(replay==null)return;
            var speeds=new[]{.5f,1f,2f,4f};float next=replay.Speed;
            if(direction>0){foreach(float speed in speeds)if(speed>replay.Speed+.01f){next=speed;break;}}
            else for(int i=speeds.Length-1;i>=0;i--)if(speeds[i]<replay.Speed-.01f){next=speeds[i];break;}
            Session.SetReplaySpeed(next);PulseHaptic(.12f,.025f);
        }
        private void PointReplayBoard(Ray ray)
        {
            if(!Board.RayToMap(ray,out var point,out var hit)){ClearPointer();return;}
            Board.Pointer=point;Board.ResolvePointerTarget(ray,point,hit,out int entity,out int resource);Board.Hovered=entity;Board.HoveredResource=resource;
            if(Quest.Active&&laser!=null)laser.SetPosition(1,hit);
        }
        private bool RoutePanel(Ray ray,bool triggerDown,bool triggerUp,bool triggerHeld,bool actionDown,bool actionUp,bool actionHeld,float depth)
            =>RoutePanelInput(ray,triggerDown,triggerUp,triggerHeld,actionDown,actionUp,actionHeld,depth,
                HotkeyInput.Read(Quest.Active?"touch:grip":"key:LeftCtrl",Quest.ActiveController).Held);
        private bool RoutePanelInput(Ray ray,bool triggerDown,bool triggerUp,bool triggerHeld,bool actionDown,bool actionUp,bool actionHeld,float depth,bool assignGroup)
        {
            float scale=Quest.Active?OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,Quest.ActiveController).x:0;
            if(panelReleasePending)
            {
                // The owner release may just have opened a section guide. Drain
                // the other alias without dismissing that newly opened dialog.
                ResetMapGestures();if(!Panel.SectionHelpOpen)Panel.CancelPointerInteraction();
                panelReleasePending=triggerHeld||actionHeld;return true;
            }
            if(panelButton!=PanelButton.None)
            {
                // The button that started the interaction owns its release. Pressing both
                // aliases must not execute twice, shorten a hold, or send an order to the map.
                bool released=panelButton==PanelButton.Trigger?triggerUp:actionUp;
                ResetMapGestures();Panel.Point(ray,false,released,depth,assignGroup,scale);
                if(released){panelButton=PanelButton.None;panelReleasePending=triggerHeld||actionHeld;}
                return true;
            }
            bool consumed=Panel.Point(ray,triggerDown||actionDown,triggerUp||actionUp,depth,assignGroup,scale);
            if(consumed)ResetMapGestures();
            if(consumed&&(triggerDown||actionDown))
            {
                panelButton=triggerDown?PanelButton.Trigger:PanelButton.Action;
                if(panelButton==PanelButton.Trigger?triggerUp:actionUp)
                {panelButton=PanelButton.None;panelReleasePending=triggerHeld||actionHeld;}
            }
            return consumed;
        }
        private void CancelPanelInput()
        {panelButton=PanelButton.None;panelReleasePending=true;Panel?.CancelPointerInteraction();}
        private bool ValidatePointerTracking(bool tracked)
        {
            if(tracked)return true;
            if(laser!=null)laser.enabled=false;
            ResetWheelRouting();CancelPanelInput();ClearPointer();
            placementInputReady=false;placementHeightDirection=0;controllerReleasePending=true;
            return false;
        }
        private bool RouteWheel(Ray ray,Vector2 stick,bool press,bool mapInput,bool triggerDown,bool triggerHeld,bool cancel,bool grip,bool gripDown,float now,bool stickHeld,bool stickReleased)
        {
            if(Wheel==null)return false;
            if(Panel!=null&&Panel.SectionHelpOpen){ResetWheelRouting();ClearPointer();return true;}
            // Wheel navigation always uses the physical trigger, independently of map
            // bindings. Infer its release here so the same router accepts synthetic edges.
            bool triggerUp=wheelTriggerHeld&&!triggerHeld;wheelTriggerHeld=triggerHeld;
            bool wasOpen=Wheel.IsOpen;
            if(!wasOpen&&(wheelReleasePending||panelButton!=PanelButton.None||Panel.IsDragging||dragging||mapInput||cancel||triggerDown||triggerHeld))
            {
                // A gesture begun over the board/console cannot turn into wheel input.
                // Closing resets the neutral-to-tilt latch after interrupted interactions.
                Wheel.Close();Wheel.TickHint(ray,Wheel.CanOpen&&!Panel.IsDragging,now);
                if(wheelReleasePending)
                {
                    wheelReleasePending=triggerHeld||grip||stickHeld;
                    ClearPointer();return true;
                }
                return false;
            }
            var previousPage=Wheel.Page;string previousItem=Wheel.HighlightedId;
            bool consumed=Wheel.ProcessUnified(ray,stick,triggerDown,triggerHeld,triggerUp,cancel,now,press,stickHeld,stickReleased);
            if(consumed)
            {
                // A page transition also highlights an item: emit only the stronger
                // level pulse. A held direction and description refresh stay silent.
                if(Wheel.IsOpen)
                {
                    if(!wasOpen||Wheel.Page!=previousPage){PulseHaptic(.28f,.055f);Session.GetComponent<AudioFeedback>()?.Navigation(true);}
                    else if(!triggerDown&&Wheel.HighlightedId!=null&&Wheel.HighlightedId!=previousItem){PulseHaptic(.12f,.025f);Session.GetComponent<AudioFeedback>()?.Navigation(false);}
                }
                ClearPointer();CancelPanelInput();wheelReleasePending=true;
                if(laser!=null)laser.enabled=!Wheel.IsOpen&&!Session.HasSystemPause&&Quest.PointerTracked;
                return true;
            }
            return false;
        }
        private void ResetWheelRouting()
        {
            replayStickReady=replayMiddleDrag=false;
            wheelTriggerHeld=false;Wheel?.Close();Wheel?.TickHint(default,false);
        }
        private void PointBoard(Ray ray,bool down,bool up,bool order,bool add)
        {
            ValidateMapGestures(add);
            bool onMap=Board.RayToMap(ray,out var point,out var worldHit);
            Board.Pointer=onMap?point:(Point?)null;
            if(onMap){Board.ResolvePointerTarget(ray,point,worldHit,out int entity,out int resource);Board.Hovered=entity;Board.HoveredResource=resource;}
            else{Board.Hovered=0;Board.HoveredResource=0;}
            // The board plane extends beyond its edges for selection only.
            if(!onMap&&!Board.RayToSelectionPlane(ray,out point)){ClearPointer();return;}
            if(deselectHoldConsumed)
            {
                // A completed hold owns the trigger until release. Its release cannot
                // turn into a short additive click and select the removed unit again.
                if(Quest.Active&&laser!=null&&onMap)laser.SetPosition(1,worldHit);
                if(up)deselectHoldConsumed=false;
                return;
            }
            if(down)
            {
                ResetActionTap();
                if(Session.HasTargetingMode)
                {ResetMapGestures();if(onMap)OrderClick(point,Board.Hovered,Board.HoveredResource,add,Time.unscaledTime);}
                else
                {
                    start=point;startedOnMap=onMap;dragging=true;pressedResource=Board.HoveredResource;pressedEntity=Board.Hovered;selectPressedAt=Time.unscaledTime;
                    dragThreshold=pressedResource>0?Mathf.Max(1.5f,.015f/Board.CellScale):1.5f;
                    areaGesture=!onMap;
                    deselectHoldCandidate=add&&onMap&&pressedEntity>0&&Session.Selection.Contains(pressedEntity);
                }
            }
            if(Quest.Active&&laser!=null&&(onMap||dragging))
                laser.SetPosition(1,onMap?worldHit:Board.Content.TransformPoint(new Vector3(point.x,0,point.y)));
            bool area=start.Distance(point)>dragThreshold||!startedOnMap||!onMap;
            if(dragging)
            {
                areaGesture|=area;area=areaGesture;
                if(!add||area||!onMap||Board.Hovered!=pressedEntity||!Session.Selection.Contains(pressedEntity))deselectHoldCandidate=false;
                if(deselectHoldCandidate&&Time.unscaledTime-selectPressedAt>=DeselectHoldTime)
                {
                    Session.Deselect(pressedEntity);deselectHoldCandidate=false;deselectHoldConsumed=!up;dragging=false;
                    ResetMapGestures();Board.DrawSelection(start,point,false);return;
                }
            }
            if(dragging)Board.DrawSelection(start,point,area);
            if(up&&dragging)
            {
                if(area)
                {ResetSelectTap();if(Board.TrySelectionBounds(start,point,out var min,out var max))Session.SelectRectangle(min,max,add);}
                else if(onMap&&startedOnMap)
                {
                    // Keep the deposit pressed through small hand tremors; a real drag
                    // still selects an area. Do not select a patch depleted meanwhile.
                    if(pressedResource>0)
                    {ResetSelectTap();if(System.Array.Exists(Session.View.resources,r=>r.id==pressedResource&&r.remaining!=0))Session.Select(0,add,pressedResource);}
                    else SelectClick(Board.Hovered,add,selectPressedAt);
                }
                dragging=false;Board.DrawSelection(start,point,false);
            }
            if(order&&onMap)OrderClick(point,Board.Hovered,Board.HoveredResource,add,Time.unscaledTime);
        }
        private void SelectClick(int entity,bool add,float now)
        {
            ValidateMapGestures(add);
            bool twice=entity>0&&entity==pressedEntity&&entity==selectEntity&&lastSelectPress>=0&&
                now>=lastSelectPress&&now-lastSelectPress<=DoubleTapWindow&&selectAdd==add&&selectSelection.SetEquals(Session.Selection);
            if(twice){ResetSelectTap();Session.SelectSameTypeOnMap(entity,add);return;}
            Session.Select(entity,add);
            ResetSelectTap();
            if(entity<=0||entity!=pressedEntity||Session.View==null||!System.Array.Exists(Session.View.entities,
                e=>e.id==entity&&e.owner==Session.View.player&&!e.remembered&&e.health>0))return;
            lastSelectPress=now;selectEntity=entity;selectAdd=add;selectSelection.UnionWith(Session.Selection);
        }
        private void OrderClick(Point point,int entity,int resource,bool add,float now)
        {
            ValidateMapGestures(add);
            ResetSelectTap();
            bool armedAttack=Session.AttackMoveMode;
            bool armedAbility=Session.PendingAbility!=null;
            bool mode=Session.HasTargetingMode||Session.CanSetRallyPoint;
            // Small hand movements must not become different orders just because
            // the table is zoomed out or contains more map cells.
            float tolerance=Quest!=null&&Quest.Active&&Board!=null
                ?Mathf.Max(1.5f,AttackTapAimTolerance/Mathf.Max(.0001f,Board.CellScale)):1.5f;
            bool nearby=actionPoint.Distance(point)<=tolerance;
            bool sameTarget=actionEntity==entity&&actionResource==resource&&(entity>0||nearby);
            bool previousGone=actionEntity>0&&Session.View!=null&&!System.Array.Exists(Session.View.entities,e=>e.id==actionEntity&&!e.remembered&&e.health>0);
            bool previousEnemyAlive=actionEntity>0&&Session.View!=null&&System.Array.Exists(Session.View.entities,
                e=>e.id==actionEntity&&e.owner!=Session.View.player&&!e.remembered&&e.health>0);
            // A moving army changes what is under the ray between the taps. Ground,
            // a friendly body, or the spot where the first target just died are still
            // the same attack gesture. Two distinct living enemies remain separate.
            // Keep a quick ground click after a direct attack on a living enemy as
            // Move: that is the retreat half of deliberate hit-and-run micro.
            if(nearby&&(actionConfirmedTarget||previousGone
                ||(!previousEnemyAlive&&(actionResource==0||resource==0))))sameTarget=true;
            bool twice=!mode&&lastActionPress>=0&&now>=lastActionPress&&now-lastActionPress<=AttackDoubleTapWindow&&
                actionSequence>0&&actionAdd==add&&sameTarget&&actionSelection.SetEquals(Session.Selection);
            int replace=twice&&add?actionSequence:0;
            bool confirmed=actionConfirmedTarget;
            ResetActionTap();
            // Target confirmation has already issued Attack Move or a spell. A quick second A
            // must not replace it with ordinary Move or append a duplicate waypoint.
            if(twice&&confirmed)return;
            // The first A acts immediately. The second converts that queued command,
            // avoiding both input latency and an extra MOVE before the queued attack.
            int sequence=Session.Context(point,entity,resource,append:add,attack:twice,replaceSequence:replace);
            if(twice||(mode&&!armedAttack&&!armedAbility)||sequence==0)return;
            lastActionPress=now;actionSequence=sequence;actionEntity=entity;actionResource=resource;actionPoint=point;actionAdd=add;
            actionConfirmedTarget=armedAttack||armedAbility;
            actionSelection.UnionWith(Session.Selection);
        }
        private void ValidateMapGestures(bool add)
        {
            if(gestureMatch!=Session.SessionIdentity||!Session.CanIssueOrders||Session.HasTargetingMode)ResetMapGestures();
            gestureMatch=Session.SessionIdentity;
            // Session removes casualties from selection on each simulation update.
            // That must not be mistaken for the player changing the selected group.
            if(lastActionPress>=0&&Session.View!=null&&!actionSelection.SetEquals(Session.Selection))
                actionSelection.RemoveWhere(id=>!System.Array.Exists(Session.View.entities,e=>e.id==id&&e.owner==Session.View.player&&e.health>0));
            if(lastActionPress>=0&&(actionAdd!=add||!actionSelection.SetEquals(Session.Selection)))ResetActionTap();
            if(lastSelectPress>=0&&(selectAdd!=add||!selectSelection.SetEquals(Session.Selection)))ResetSelectTap();
        }
        private void ResetActionTap(){lastActionPress=-1;actionSequence=0;actionConfirmedTarget=false;actionSelection.Clear();}
        private void ResetSelectTap(){lastSelectPress=-1;selectEntity=0;selectSelection.Clear();}
        private void ResetMapGestures(){ResetActionTap();ResetSelectTap();gestureMatch=null;}
        private void ChangeInputController(OVRInput.Controller controller)
        {
            placementInputReady=false;placementHeightDirection=0;
            StopHaptic();
            inputController=controller;controllerReleasePending=true;
            ResetWheelRouting();
            CancelPanelInput();wheelReleasePending=true;ClearPointer();
        }
        private void Haptic(bool success)
        {PulseHaptic(success ? .2f : .65f,.07f);}
        public void TutorialTransitionHint()
        {
            if(Session==null||Session.HasSystemPause||Quest==null||!Quest.PointerTracked)return;
            PulseHaptic(.28f,.1f);
        }
        private void PulseHaptic(float amplitude,float duration)
        {
            if(!isActiveAndEnabled||Quest==null||!Quest.Active)return;
            var controller=Quest.ActiveController;
            if(hapticController!=OVRInput.Controller.None&&hapticController!=controller)StopHaptic();
            OVRInput.SetControllerVibration(.5f,amplitude,controller);
            hapticController=controller;hapticEnd=Time.unscaledTime+duration;
        }
        private void UpdateHaptics()
        {if(hapticController!=OVRInput.Controller.None&&Time.unscaledTime>=hapticEnd)StopHaptic();}
        private void StopHaptic()
        {
            if(hapticController!=OVRInput.Controller.None)OVRInput.SetControllerVibration(0,0,hapticController);
            hapticController=OVRInput.Controller.None;hapticEnd=0;
        }
        private void OnApplicationFocus(bool focused)
        {
            if(!focused)
            {
#if FRONTIER_BROWSER
                mobileGesture.CancelUntilRelease();ClearMobileLasso();
#endif
                placementInputReady=false;placementHeightDirection=0;StopHaptic();ResetWheelRouting();CancelPanelInput();wheelReleasePending=true;ClearPointer();
            }
        }
#if FRONTIER_BROWSER
        private void OnApplicationPause(bool paused)
        {if(paused){mobileGesture.CancelUntilRelease();ClearMobileLasso();}}
#endif
        private void OnDisable()
        {
#if FRONTIER_BROWSER
            mobileGesture.CancelUntilRelease();ClearMobileLasso();
#endif
            placementInputReady=false;placementHeightDirection=0;controllerVisuals?.Hide();if(laser!=null)laser.enabled=false;StopHaptic();ResetWheelRouting();CancelPanelInput();wheelReleasePending=true;ClearPointer();
        }
        private void OnDestroy()
        {
#if FRONTIER_BROWSER
            DisposeMobileLasso();
#endif
            if(Session!=null)Session.Feedback-=Haptic;controllerVisuals?.Dispose();if(laser!=null){if(Application.isPlaying){Destroy(laser.sharedMaterial);Destroy(laser.gameObject);}else{DestroyImmediate(laser.sharedMaterial);DestroyImmediate(laser.gameObject);}}
        }
        private void ClearPointer()
        {ResetMapGestures();dragging=deselectHoldCandidate=deselectHoldConsumed=areaGesture=false;pressedResource=pressedEntity=0;if(Board!=null){Board.Hovered=0;Board.HoveredResource=0;Board.Pointer=null;Board.DrawSelection(start,start,false);}}
    }
}
