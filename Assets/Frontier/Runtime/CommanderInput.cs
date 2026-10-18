using Frontier.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frontier.Runtime
{
    public sealed class CommanderInput:MonoBehaviour
    {
        public GameSession Session;public BoardView Board;public CommandPanel Panel;public QuestBridge Quest;public ActionWheel Wheel;
        private Point start;private bool dragging, startedOnMap;private LineRenderer laser;private float hapticEnd;
        private bool wheelReleasePending;
        private enum PanelButton { None, Trigger, Action }
        private PanelButton panelButton;
        private bool panelReleasePending;
        private float lastGripPress=-1;
        private Simulation gripTapMatch;
        private const float DoubleGripWindow=.35f;
        private bool groupRecallReady;
        private Simulation groupRecallMatch;
        private bool stickHoldPending,stickHoldReady;
        private Simulation stickHoldMatch;
        private readonly HashSet<int> stickHoldSelection=new HashSet<int>();
        private int pressedResource;
        private int pressedEntity;
        private float selectPressedAt;
        private bool deselectHoldCandidate,deselectHoldConsumed,areaGesture;
        private const float DeselectHoldTime=.5f;
        private float dragThreshold=1.5f;
        private const float DoubleTapWindow=.35f;
        private float lastActionPress=-1,lastSelectPress=-1;
        private int actionSequence,actionEntity,actionResource,selectEntity;
        private Point actionPoint;
        private bool actionAdd,selectAdd;
        private Simulation gestureMatch;
        private readonly HashSet<int> actionSelection=new HashSet<int>(),selectSelection=new HashSet<int>();
        private void Start()
        {
            Panel.PlaceRequested=Quest.BeginPlacement;Session.Feedback+=Haptic;
            var go=new GameObject("Pointer ray");laser=go.AddComponent<LineRenderer>();laser.sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));laser.sharedMaterial.color=new Color(.3f,.95f,.94f);laser.positionCount=2;laser.widthMultiplier=.002f;laser.enabled=Quest.Active;
        }
        private void Update()
        {
            if(Board.Content==null){ResetMapGestures();return;}
            bool down=false,up=false,order=false,add=false,cancel=false,toggleMenu=false;Vector2 pan=Vector2.zero;float zoom=0,menuDepth=0;Ray ray;
            Vector2 primaryStick=Vector2.zero;bool wheelPressed=false,wheelHeld=false,wheelReleased=false,triggerHeld=false,gripDown=false,actionUp=false,actionHeld=false;
            if(Quest.Active)
            {
                // All gameplay input is on the physical right controller. The left is unassigned.
                const OVRInput.Controller primary=OVRInput.Controller.RTouch;
                ray=Quest.PointerRay();down=OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger,primary);up=OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger,primary);
                triggerHeld=OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger,primary);
                add=OVRInput.Get(OVRInput.Button.PrimaryHandTrigger,primary);order=OVRInput.GetDown(OVRInput.Button.One,primary);cancel=OVRInput.GetDown(OVRInput.Button.Two,primary);
                actionUp=OVRInput.GetUp(OVRInput.Button.One,primary);actionHeld=OVRInput.Get(OVRInput.Button.One,primary);
                gripDown=OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger,primary);
                primaryStick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,primary);
                wheelPressed=OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick,primary);
                wheelHeld=OVRInput.Get(OVRInput.Button.PrimaryThumbstick,primary);
                wheelReleased=OVRInput.GetUp(OVRInput.Button.PrimaryThumbstick,primary);
                menuDepth=primaryStick.y;
                if(Time.unscaledTime>hapticEnd)OVRInput.SetControllerVibration(0,0,primary);
                laser.enabled=true;
                laser.SetPosition(0,ray.origin);laser.SetPosition(1,ray.GetPoint(2));
            }
            else
            {
                if(Mouse.current==null)return;ray=Quest.Camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                down=Mouse.current.leftButton.wasPressedThisFrame;up=Mouse.current.leftButton.wasReleasedThisFrame;order=Mouse.current.rightButton.wasPressedThisFrame;
                triggerHeld=Mouse.current.leftButton.isPressed;
                zoom=Mouse.current.scroll.ReadValue().y*.04f;menuDepth=zoom;
                if(Keyboard.current!=null)
                {
                    var k=Keyboard.current;add=k.leftShiftKey.isPressed||k.rightShiftKey.isPressed;cancel=k.escapeKey.wasPressedThisFrame;
                    pan=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                    toggleMenu=k.spaceKey.wasPressedThisFrame;if(k.fKey.wasPressedThisFrame)Board.Fit();
                    if(k.mKey.wasPressedThisFrame)Panel.Recall();
                    if(Panel.AcceptsGameInput)
                    {bool save=k.leftCtrlKey.isPressed||k.rightCtrlKey.isPressed;if(k.digit1Key.wasPressedThisFrame)Session.Group(0,save);if(k.digit2Key.wasPressedThisFrame)Session.Group(1,save);if(k.digit3Key.wasPressedThisFrame)Session.Group(2,save);if(k.digit4Key.wasPressedThisFrame)Session.Group(3,save);}
                }
            }
            ValidateMapGestures(add);
            if(Quest.Placing)
            {Wheel?.Close();ResetGripTap();ResetGroupRecall();ResetStickHold();CancelPanelInput();ClearPointer();if(cancel||toggleMenu)Quest.CancelPlacement();else Quest.PlaceRay(ray,add?primaryStick.y:0,add?Vector2.zero:primaryStick,down);return;}
            if(Quest.Active&&RouteWheel(ray,primaryStick,wheelPressed,order,down,triggerHeld,cancel,add,gripDown,Time.unscaledTime,wheelHeld,wheelReleased))return;
            // Back/pause must work even when the pointer is over a menu button.
            if(cancel)
            {if(Panel.AcceptsGameInput&&(Session.BuildDefinition!=null||Session.AttackMoveMode))Session.CancelMode();else Panel.Back();CancelPanelInput();ClearPointer();return;}
            if(toggleMenu){Panel.ToggleMenu();CancelPanelInput();ClearPointer();return;}
            if(RoutePanel(ray,down,up,triggerHeld,Quest.Active&&order,actionUp,actionHeld,menuDepth)){ClearPointer();return;}
            // Quest keeps the confirmed board view fixed. Navigation remains a desktop
            // development control; the right stick operates the wheel, placement or a grabbed menu.
            if(!Quest.Active&&(Panel.AcceptsGameInput||Panel.Page==CommandPanel.MenuPage.MatchSetup))
                Board.Navigate(pan,zoom,zoom!=0?1:Time.unscaledDeltaTime);
            if(!Panel.AcceptsGameInput){ClearPointer();return;}
            PointBoard(ray,down,up,order,add);
        }
        private bool RoutePanel(Ray ray,bool triggerDown,bool triggerUp,bool triggerHeld,bool actionDown,bool actionUp,bool actionHeld,float depth)
        {
            if(panelReleasePending)
            {
                ResetMapGestures();Panel.CancelPointerInteraction();panelReleasePending=triggerHeld||actionHeld;return true;
            }
            if(panelButton!=PanelButton.None)
            {
                // The button that started the interaction owns its release. Pressing both
                // aliases must not execute twice, shorten a hold, or send an order to the map.
                bool released=panelButton==PanelButton.Trigger?triggerUp:actionUp;
                ResetMapGestures();Panel.Point(ray,false,released,depth);
                if(released){panelButton=PanelButton.None;panelReleasePending=triggerHeld||actionHeld;}
                return true;
            }
            bool consumed=Panel.Point(ray,triggerDown||actionDown,triggerUp||actionUp,depth);
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
        private bool RouteWheel(Ray ray,Vector2 stick,bool press,bool confirm,bool triggerDown,bool triggerHeld,bool cancel,bool grip,bool gripDown,float now,bool stickHeld,bool stickReleased)
        {
            if(Wheel==null)return false;
            if(gripTapMatch!=Session.Match)ResetGripTap();
            if(stickHoldMatch!=Session.Match||!Wheel.CanOpen||!stickHoldSelection.SetEquals(Session.Selection)||cancel||confirm||triggerDown||triggerHeld||(!stickHeld&&!stickReleased))
                ResetStickHold();
            if(groupRecallMatch!=Session.Match){ResetGroupRecall();groupRecallMatch=Session.Match;}
            // Observe neutral in free gameplay before accepting a direction. A stick
            // held through placement, menu dragging or wheel dismissal must not reopen
            // groups. Press-and-tilt saving and open action wheels keep priority.
            bool freeStick=Wheel.CanOpen&&!Wheel.IsOpen&&!wheelReleasePending&&panelButton==PanelButton.None&&!dragging
                &&!press&&!stickHeld&&!stickReleased&&!confirm&&!cancel&&!triggerDown&&!triggerHeld&&!gripDown;
            if(!freeStick)groupRecallReady=false;
            else if(stick.magnitude<.25f)groupRecallReady=true;
            else if(groupRecallReady&&stick.magnitude>=.45f)
            {
                ResetGroupRecall();ResetGripTap();ResetStickHold();
                Wheel.OpenGroups(ray,stick,false);
                ClearPointer();CancelPanelInput();wheelReleasePending=true;
                if(laser!=null)laser.enabled=false;
                return true;
            }
            if(press)
            {
                ResetStickHold();
                if(Wheel.CanOpen&&stickHeld&&!cancel&&!confirm&&!triggerHeld)
                {
                    stickHoldPending=true;stickHoldReady=stick.magnitude<.25f;stickHoldMatch=Session.Match;
                    stickHoldSelection.UnionWith(Session.Selection);
                }
            }
            if(stickHoldPending&&stickHeld&&stick.magnitude<.25f)stickHoldReady=true;
            bool startSaving=stickHoldPending&&stickHoldReady&&(stickHeld||stickReleased)&&stick.magnitude>=.45f&&!Wheel.GroupSaveMode;
            if(startSaving){Wheel.OpenGroups(ray,stick,true);ResetGroupRecall();ResetGripTap();}
            bool doubleGrip=false;
            if(Wheel.IsOpen)
            {
                // Only fresh presses act inside the wheel: the opening grip stays harmless
                // when held, and a held trigger cannot repeatedly execute submenu actions.
                confirm|=triggerDown;if(!Wheel.InGroupMenu){cancel|=gripDown;if(gripDown)ResetStickHold();}ResetGripTap();
            }
            else if(!Wheel.CanOpen||wheelReleasePending||press||confirm||cancel||triggerDown||triggerHeld)
                ResetGripTap();
            else if(gripDown)
            {
                doubleGrip=lastGripPress>=0&&now>=lastGripPress&&now-lastGripPress<=DoubleGripWindow;
                lastGripPress=doubleGrip?-1:now;gripTapMatch=Session.Match;
            }
            bool wasOpen=Wheel.IsOpen;
            bool consumed=Wheel.Process(ray,stick,!startSaving&&(press||doubleGrip),confirm,cancel,stickHeld,stickReleased);
            if(stickReleased||(wasOpen&&!Wheel.IsOpen&&!press))ResetStickHold();
            if(consumed)
            {
                ResetGroupRecall();
                ResetGripTap();
                ClearPointer();CancelPanelInput();wheelReleasePending=true;
                if(laser!=null)laser.enabled=!Wheel.IsOpen;
                return true;
            }
            // Drain both controls and their release before resuming map/menu interaction.
            if(wheelReleasePending){wheelReleasePending=triggerHeld||grip||stickHeld;ClearPointer();return true;}
            return false;
        }
        private void ResetGripTap(){lastGripPress=-1;gripTapMatch=null;}
        private void ResetGroupRecall(){groupRecallReady=false;groupRecallMatch=null;}
        private void ResetStickHold(){stickHoldPending=stickHoldReady=false;stickHoldMatch=null;stickHoldSelection.Clear();}
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
                if(Session.BuildDefinition!=null||Session.AttackMoveMode)
                {ResetMapGestures();if(onMap)Session.Context(point,Board.Hovered,Board.HoveredResource,append:add);}
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
            if(twice){ResetSelectTap();Session.SelectNearbySameType(entity,add);return;}
            Session.Select(entity,add);
            ResetSelectTap();
            if(entity<=0||entity!=pressedEntity||Session.View==null||!System.Array.Exists(Session.View.entities,
                e=>e.id==entity&&e.owner==0&&!Session.Catalog[e.definition].building))return;
            lastSelectPress=now;selectEntity=entity;selectAdd=add;selectSelection.UnionWith(Session.Selection);
        }
        private void OrderClick(Point point,int entity,int resource,bool add,float now)
        {
            ValidateMapGestures(add);
            ResetSelectTap();
            bool mode=Session.BuildDefinition!=null||Session.AttackMoveMode||Session.CanSetRallyPoint;
            bool sameTarget=actionEntity==entity&&actionResource==resource&&(entity>0||actionPoint.Distance(point)<=1.5f);
            bool twice=!mode&&lastActionPress>=0&&now>=lastActionPress&&now-lastActionPress<=DoubleTapWindow&&
                actionSequence>0&&actionAdd==add&&sameTarget&&actionSelection.SetEquals(Session.Selection);
            int replace=twice&&add?actionSequence:0;
            ResetActionTap();
            // The first A acts immediately. The second converts that queued command,
            // avoiding both input latency and an extra MOVE before the queued attack.
            int sequence=Session.Context(point,entity,resource,append:add,attack:twice,replaceSequence:replace);
            if(twice||mode||sequence==0)return;
            lastActionPress=now;actionSequence=sequence;actionEntity=entity;actionResource=resource;actionPoint=point;actionAdd=add;
            actionSelection.UnionWith(Session.Selection);
        }
        private void ValidateMapGestures(bool add)
        {
            if(gestureMatch!=Session.Match||Session.IsPaused||Session.BuildDefinition!=null||Session.AttackMoveMode)ResetMapGestures();
            gestureMatch=Session.Match;
            if(lastActionPress>=0&&(actionAdd!=add||!actionSelection.SetEquals(Session.Selection)))ResetActionTap();
            if(lastSelectPress>=0&&(selectAdd!=add||!selectSelection.SetEquals(Session.Selection)))ResetSelectTap();
        }
        private void ResetActionTap(){lastActionPress=-1;actionSequence=0;actionSelection.Clear();}
        private void ResetSelectTap(){lastSelectPress=-1;selectEntity=0;selectSelection.Clear();}
        private void ResetMapGestures(){ResetActionTap();ResetSelectTap();gestureMatch=null;}
        private void Haptic(bool success)
        {if(!Quest.Active)return;OVRInput.SetControllerVibration(.5f,success ? .2f : .65f,OVRInput.Controller.RTouch);hapticEnd=Time.unscaledTime+.07f;}
        private void OnApplicationFocus(bool focused)
        {if(!focused){Wheel?.Close();ResetGripTap();ResetGroupRecall();ResetStickHold();CancelPanelInput();wheelReleasePending=true;ClearPointer();}}
        private void OnDisable(){Wheel?.Close();ResetGripTap();ResetGroupRecall();ResetStickHold();CancelPanelInput();wheelReleasePending=true;ClearPointer();}
        private void ClearPointer()
        {ResetMapGestures();dragging=deselectHoldCandidate=deselectHoldConsumed=areaGesture=false;pressedResource=pressedEntity=0;if(Board!=null){Board.Hovered=0;Board.HoveredResource=0;Board.Pointer=null;Board.DrawSelection(start,start,false);}}
    }
}
