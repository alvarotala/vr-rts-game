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
        private OVRInput.Controller inputController=OVRInput.Controller.None;
        private bool controllerReleasePending;
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
        private float lastActionPress=-1,lastSelectPress=-1;
        private int actionSequence,actionEntity,actionResource,selectEntity;
        private Point actionPoint;
        private bool actionAdd,selectAdd,actionConfirmedTarget;
        private Simulation gestureMatch;
        private readonly HashSet<int> actionSelection=new HashSet<int>(),selectSelection=new HashSet<int>();
        private void Start()
        {
            Panel.PlaceRequested=Quest.BeginPlacement;Session.Feedback+=Haptic;
            var go=new GameObject("Pointer ray");laser=go.AddComponent<LineRenderer>();laser.sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));laser.sharedMaterial.color=new Color(.3f,.95f,.94f);laser.positionCount=2;laser.widthMultiplier=.002f;laser.enabled=Quest.Active;
        }
        private void LateUpdate()
        {
            // Read after input routing so pointer mode changes appear in this frame.
            if(laser!=null&&Board!=null)laser.sharedMaterial.color=Board.PointerColor;
        }
        private void Update()
        {
            if(Board.Content==null){ResetMapGestures();Wheel?.TickHint(default,false);return;}
            bool down=false,up=false,order=false,add=false,cancel=false,toggleMenu=false;Vector2 pan=Vector2.zero;float zoom=0,menuDepth=0;Ray ray;
            Vector2 primaryStick=Vector2.zero;bool wheelPressed=false,wheelHeld=false,wheelReleased=false,triggerHeld=false,gripDown=false;
            bool gameplay=Panel.AcceptsGameInput||Quest.Placing;
            var bindings=Panel.Bindings;
            if(Quest.Active)
            {
                // Buttons, axes and the pointer always come from the chosen hand.
                var primary=Quest.ActiveController;
                if(inputController!=primary)ChangeInputController(primary);
                ray=Quest.PointerRay();
                var select=HotkeyInput.Read(gameplay?bindings.Get(true,"select"):"touch:trigger",primary);
                var action=HotkeyInput.Read(gameplay?bindings.Get(true,"order"):"touch:a",primary);
                var modifier=HotkeyInput.Read(bindings,true,"add");var wheel=HotkeyInput.Read(bindings,true,"wheel");
                down=select.Down;up=select.Up;triggerHeld=select.Held;
                add=modifier.Held;gripDown=modifier.Down;order=action.Down;
                cancel=HotkeyInput.Read(gameplay?bindings.Get(true,"back"):"touch:b",primary).Down;
                primaryStick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,primary);
                wheelPressed=wheel.Down;wheelHeld=wheel.Held;wheelReleased=wheel.Up;
                menuDepth=primaryStick.y;
                if(Time.unscaledTime>hapticEnd)OVRInput.SetControllerVibration(0,0,primary);
                laser.enabled=true;
                laser.SetPosition(0,ray.origin);laser.SetPosition(1,ray.GetPoint(2));
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
            {ResetWheelRouting();CancelPanelInput();ClearPointer();if(cancel||toggleMenu)Quest.CancelPlacement();else Quest.PlaceRay(ray,add?primaryStick.y:0,add?Vector2.zero:primaryStick,down);return;}
            var menuSelect=HotkeyInput.Read(Quest.Active?"touch:trigger":"mouse:left",Quest.ActiveController);
            var menuAction=Quest.Active?HotkeyInput.Read("touch:a",Quest.ActiveController):default;
            bool uiPointerInput=menuSelect.Down||menuSelect.Up||menuSelect.Held||menuAction.Down||menuAction.Up||menuAction.Held||panelButton!=PanelButton.None||Panel.IsDragging;
            bool interactingWithPanel=panelButton!=PanelButton.None||Panel.IsDragging||(uiPointerInput&&Panel.ContainsPointer(ray));
            if(!Quest.Active&&Panel.Page!=CommandPanel.MenuPage.Hotkeys&&!interactingWithPanel&&!panelReleasePending)
            {
                if(HotkeyInput.Read(bindings,false,"menu").Down)Panel.Recall();
                if(Panel.AcceptsGameInput||Panel.Page==CommandPanel.MenuPage.MatchSetup)
                {if(HotkeyInput.Read(bindings,false,"fit").Down)Board.Fit();}
                if(Panel.AcceptsGameInput)
                {
                    bool save=HotkeyInput.Read(bindings,false,"save").Held;
                    for(int i=0;i<GameSession.ControlGroupCount;i++)if(HotkeyInput.Read(bindings,false,"group"+(i+1)).Down)Session.Group(i,save);
                }
            }
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
            // Pointer UI uses stable controls even after gameplay selection is rebound.
            if(RoutePanel(ray,menuSelect.Down,menuSelect.Up,menuSelect.Held,menuAction.Down,menuAction.Up,menuAction.Held,menuDepth)){ClearPointer();return;}
            // Quest keeps the confirmed board view fixed. Navigation remains a desktop
            // development control; the active stick operates the wheel, placement or a grabbed menu.
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
        private bool RouteWheel(Ray ray,Vector2 stick,bool press,bool mapInput,bool triggerDown,bool triggerHeld,bool cancel,bool grip,bool gripDown,float now,bool stickHeld,bool stickReleased)
        {
            if(Wheel==null)return false;
            // Wheel navigation always uses the physical trigger, independently of map
            // bindings. Infer its release here so the same router accepts synthetic edges.
            bool triggerUp=wheelTriggerHeld&&!triggerHeld;wheelTriggerHeld=triggerHeld;
            bool wasOpen=Wheel.IsOpen;
            if(!wasOpen&&(wheelReleasePending||panelButton!=PanelButton.None||Panel.IsDragging||dragging||mapInput||cancel||triggerDown||triggerHeld))
            {
                // A gesture begun over the board/console cannot turn into wheel input.
                // Closing resets the neutral-to-tilt latch after interrupted interactions.
                Wheel.Close();Wheel.TickHint(ray,Wheel.CanOpen&&!Panel.IsDragging);
                if(wheelReleasePending)
                {
                    wheelReleasePending=triggerHeld||grip||stickHeld;
                    ClearPointer();return true;
                }
                return false;
            }
            bool consumed=Wheel.ProcessUnified(ray,stick,triggerDown,triggerHeld,triggerUp,cancel,now,press,stickHeld,stickReleased);
            if(consumed)
            {
                ClearPointer();CancelPanelInput();wheelReleasePending=true;
                if(laser!=null)laser.enabled=!Wheel.IsOpen;
                return true;
            }
            return false;
        }
        private void ResetWheelRouting()
        {
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
            bool sameTarget=actionEntity==entity&&actionResource==resource&&(entity>0||actionPoint.Distance(point)<=1.5f);
            bool nearby=actionPoint.Distance(point)<=1.5f;
            bool previousGone=actionEntity>0&&Session.View!=null&&!System.Array.Exists(Session.View.entities,e=>e.id==actionEntity&&!e.remembered&&e.health>0);
            bool previousEnemyAlive=actionEntity>0&&Session.View!=null&&System.Array.Exists(Session.View.entities,
                e=>e.id==actionEntity&&e.owner!=Session.View.player&&!e.remembered&&e.health>0);
            // A moving army changes what is under the ray between the taps. Ground,
            // a friendly body, or the spot where the first target just died are still
            // the same attack gesture. Two distinct living enemies remain separate.
            // Keep a quick ground click after a direct attack on a living enemy as
            // Move: that is the retreat half of deliberate hit-and-run micro.
            if(nearby&&(actionConfirmedTarget||previousGone||(actionEntity==0&&actionResource==0)
                ||(entity==0&&resource==0&&!previousEnemyAlive)))sameTarget=true;
            bool twice=!mode&&lastActionPress>=0&&now>=lastActionPress&&now-lastActionPress<=DoubleTapWindow&&
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
            if(gestureMatch!=Session.Match||Session.IsPaused||Session.HasTargetingMode)ResetMapGestures();
            gestureMatch=Session.Match;
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
            if(inputController!=OVRInput.Controller.None)OVRInput.SetControllerVibration(0,0,inputController);
            inputController=controller;hapticEnd=0;controllerReleasePending=true;
            ResetWheelRouting();
            CancelPanelInput();wheelReleasePending=true;ClearPointer();
        }
        private void Haptic(bool success)
        {if(!Quest.Active)return;OVRInput.SetControllerVibration(.5f,success ? .2f : .65f,Quest.ActiveController);hapticEnd=Time.unscaledTime+.07f;}
        private void OnApplicationFocus(bool focused)
        {if(!focused){ResetWheelRouting();CancelPanelInput();wheelReleasePending=true;ClearPointer();}}
        private void OnDisable(){ResetWheelRouting();CancelPanelInput();wheelReleasePending=true;ClearPointer();}
        private void ClearPointer()
        {ResetMapGestures();dragging=deselectHoldCandidate=deselectHoldConsumed=areaGesture=false;pressedResource=pressedEntity=0;if(Board!=null){Board.Hovered=0;Board.HoveredResource=0;Board.Pointer=null;Board.DrawSelection(start,start,false);}}
    }
}
