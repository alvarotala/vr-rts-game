using Frontier.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frontier.Runtime
{
    public sealed class CommanderInput:MonoBehaviour
    {
        public GameSession Session;public BoardView Board;public CommandPanel Panel;public QuestBridge Quest;public ActionWheel Wheel;
        private Point start;private bool dragging, startedOnMap;private LineRenderer laser;private float hapticEnd;
        private bool wheelReleasePending;
        private float lastGripPress=-1;
        private Simulation gripTapMatch;
        private const float DoubleGripWindow=.35f;
        private int pressedResource;
        private float dragThreshold=1.5f;
        private void Start()
        {
            Panel.PlaceRequested=Quest.BeginPlacement;Session.Feedback+=Haptic;
            var go=new GameObject("Pointer ray");laser=go.AddComponent<LineRenderer>();laser.sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));laser.sharedMaterial.color=new Color(.3f,.95f,.94f);laser.positionCount=2;laser.widthMultiplier=.002f;laser.enabled=Quest.Active;
        }
        private void Update()
        {
            if(Board.Content==null)return;
            bool down=false,up=false,order=false,add=false,cancel=false,toggleMenu=false;Vector2 pan=Vector2.zero;float zoom=0,menuDepth=0;Ray ray;
            Vector2 primaryStick=Vector2.zero;bool wheelPressed=false,triggerHeld=false,gripDown=false;
            if(Quest.Active)
            {
                // All gameplay input is on the physical right controller. The left is unassigned.
                const OVRInput.Controller primary=OVRInput.Controller.RTouch;
                ray=Quest.PointerRay();down=OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger,primary);up=OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger,primary);
                triggerHeld=OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger,primary);
                add=OVRInput.Get(OVRInput.Button.PrimaryHandTrigger,primary);order=OVRInput.GetDown(OVRInput.Button.One,primary);cancel=OVRInput.GetDown(OVRInput.Button.Two,primary);
                gripDown=OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger,primary);
                primaryStick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,primary);
                wheelPressed=OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick,primary);
                menuDepth=primaryStick.y;
                if(Time.unscaledTime>hapticEnd)OVRInput.SetControllerVibration(0,0,primary);
                laser.enabled=true;
                laser.SetPosition(0,ray.origin);laser.SetPosition(1,ray.GetPoint(2));
            }
            else
            {
                if(Mouse.current==null)return;ray=Quest.Camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                down=Mouse.current.leftButton.wasPressedThisFrame;up=Mouse.current.leftButton.wasReleasedThisFrame;order=Mouse.current.rightButton.wasPressedThisFrame;
                zoom=Mouse.current.scroll.ReadValue().y*.04f;menuDepth=zoom;
                if(Keyboard.current!=null)
                {
                    var k=Keyboard.current;add=k.leftShiftKey.isPressed||k.rightShiftKey.isPressed;cancel=k.escapeKey.wasPressedThisFrame;
                    pan=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                    toggleMenu=k.spaceKey.wasPressedThisFrame;if(k.fKey.wasPressedThisFrame)Board.Fit();
                    if(k.mKey.wasPressedThisFrame)Panel.Recall();
                    if(Panel.AcceptsGameInput)
                    {if(k.digit1Key.wasPressedThisFrame)Session.Group(0,k.leftCtrlKey.isPressed);if(k.digit2Key.wasPressedThisFrame)Session.Group(1,k.leftCtrlKey.isPressed);if(k.digit3Key.wasPressedThisFrame)Session.Group(2,k.leftCtrlKey.isPressed);}
                }
            }
            if(Quest.Placing)
            {Wheel?.Close();ResetGripTap();ClearPointer();if(cancel||toggleMenu)Quest.CancelPlacement();else Quest.PlaceRay(ray,add?primaryStick.y:0,add?Vector2.zero:primaryStick,down);return;}
            if(Quest.Active&&RouteWheel(ray,primaryStick,wheelPressed,order,down,triggerHeld,cancel,add,gripDown,Time.unscaledTime))return;
            // Back/pause must work even when the pointer is over a menu button.
            if(cancel)
            {if(Panel.AcceptsGameInput&&(Session.BuildDefinition!=null||Session.AttackMoveMode))Session.CancelMode();else Panel.Back();ClearPointer();return;}
            if(toggleMenu){Panel.ToggleMenu();ClearPointer();return;}
            if(Panel.Point(ray,down,up,menuDepth)){ClearPointer();return;}
            // Quest keeps the confirmed board view fixed. Navigation remains a desktop
            // development control; the right stick operates the wheel, placement or a grabbed menu.
            if(!Quest.Active&&(Panel.AcceptsGameInput||Panel.Page==CommandPanel.MenuPage.MatchSetup))
                Board.Navigate(pan,zoom,zoom!=0?1:Time.unscaledDeltaTime);
            if(!Panel.AcceptsGameInput){ClearPointer();return;}
            PointBoard(ray,down,up,order,add);
        }
        private bool RouteWheel(Ray ray,Vector2 stick,bool press,bool confirm,bool triggerDown,bool triggerHeld,bool cancel,bool grip,bool gripDown,float now)
        {
            if(Wheel==null)return false;
            if(gripTapMatch!=Session.Match)ResetGripTap();
            if(press&&grip&&!Wheel.IsOpen)
            {ResetGripTap();Panel.Recall();ClearPointer();wheelReleasePending=true;return true;}
            bool doubleGrip=false;
            if(Wheel.IsOpen)
            {
                // Only fresh presses act inside the wheel: the opening grip stays harmless
                // when held, and a held trigger cannot repeatedly execute submenu actions.
                confirm|=triggerDown;cancel|=gripDown;ResetGripTap();
            }
            else if(!Wheel.CanOpen||wheelReleasePending||press||confirm||cancel||triggerDown||triggerHeld)
                ResetGripTap();
            else if(gripDown)
            {
                doubleGrip=lastGripPress>=0&&now>=lastGripPress&&now-lastGripPress<=DoubleGripWindow;
                lastGripPress=doubleGrip?-1:now;gripTapMatch=Session.Match;
            }
            if(Wheel.Process(ray,stick,press||doubleGrip,confirm,cancel))
            {
                ResetGripTap();
                ClearPointer();Panel.CancelPointerInteraction();wheelReleasePending=true;
                if(laser!=null)laser.enabled=!Wheel.IsOpen;
                return true;
            }
            // Drain both controls and their release before resuming map/menu interaction.
            if(wheelReleasePending){wheelReleasePending=triggerHeld||grip;ClearPointer();return true;}
            return false;
        }
        private void ResetGripTap(){lastGripPress=-1;gripTapMatch=null;}
        private void PointBoard(Ray ray,bool down,bool up,bool order,bool add)
        {
            bool onMap=Board.RayToMap(ray,out var point,out var worldHit);
            Board.Pointer=onMap?point:(Point?)null;
            if(onMap){Board.ResolvePointerTarget(ray,point,worldHit,out int entity,out int resource);Board.Hovered=entity;Board.HoveredResource=resource;}
            else{Board.Hovered=0;Board.HoveredResource=0;}
            // The board plane extends beyond its edges for selection only.
            if(!onMap&&!Board.RayToSelectionPlane(ray,out point)){ClearPointer();return;}
            if(down)
            {
                if(Session.BuildDefinition!=null||Session.AttackMoveMode)
                {if(onMap)Session.Context(point,Board.Hovered,Board.HoveredResource);}
                else
                {
                    start=point;startedOnMap=onMap;dragging=true;pressedResource=Board.HoveredResource;
                    dragThreshold=pressedResource>0?Mathf.Max(1.5f,.015f/Board.CellScale):1.5f;
                }
            }
            if(Quest.Active&&laser!=null&&(onMap||dragging))
                laser.SetPosition(1,onMap?worldHit:Board.Content.TransformPoint(new Vector3(point.x,0,point.y)));
            bool area=start.Distance(point)>dragThreshold||!startedOnMap||!onMap;
            if(dragging)Board.DrawSelection(start,point,area);
            if(up&&dragging)
            {
                if(area)
                {if(Board.TrySelectionBounds(start,point,out var min,out var max))Session.SelectRectangle(min,max,add);}
                else if(onMap&&startedOnMap)
                {
                    // Keep the deposit pressed through small hand tremors; a real drag
                    // still selects an area. Do not select a patch depleted meanwhile.
                    if(pressedResource>0)
                    {if(System.Array.Exists(Session.View.resources,r=>r.id==pressedResource&&r.remaining!=0))Session.Select(0,add,pressedResource);}
                    else Session.Select(Board.Hovered,add,Board.HoveredResource);
                }
                dragging=false;Board.DrawSelection(start,point,false);
            }
            if(order&&onMap)Session.Context(point,Board.Hovered,Board.HoveredResource);
        }
        private void Haptic(bool success)
        {if(!Quest.Active)return;OVRInput.SetControllerVibration(.5f,success ? .2f : .65f,OVRInput.Controller.RTouch);hapticEnd=Time.unscaledTime+.07f;}
        private void OnApplicationFocus(bool focused)
        {if(!focused){Wheel?.Close();ResetGripTap();wheelReleasePending=true;ClearPointer();}}
        private void OnDisable(){Wheel?.Close();ResetGripTap();wheelReleasePending=true;ClearPointer();}
        private void ClearPointer()
        {dragging=false;pressedResource=0;if(Board!=null){Board.Hovered=0;Board.Pointer=null;Board.DrawSelection(start,start,false);}}
    }
}
