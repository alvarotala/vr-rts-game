using Frontier.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frontier.Runtime
{
    public sealed class CommanderInput:MonoBehaviour
    {
        public GameSession Session;public BoardView Board;public CommandPanel Panel;public QuestBridge Quest;
        private Point start;private bool dragging, startedOnMap;private LineRenderer laser;private float hapticEnd;
        private void Start()
        {
            Panel.PlaceRequested=Quest.BeginPlacement;Session.Feedback+=Haptic;
            var go=new GameObject("Pointer ray");laser=go.AddComponent<LineRenderer>();laser.sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));laser.sharedMaterial.color=new Color(.3f,.95f,.94f);laser.positionCount=2;laser.widthMultiplier=.002f;laser.enabled=Quest.Active;
        }
        private void Update()
        {
            if(Board.Content==null)return;
            bool down=false,up=false,order=false,add=false,cancel=false,toggleMenu=false;Vector2 pan=Vector2.zero;float zoom=0,menuDepth=0;Ray ray;
            Vector2 primaryStick=Vector2.zero,secondaryStick=Vector2.zero;
            if(Quest.Active)
            {
                var primary=Session.LeftHanded?OVRInput.Controller.LTouch:OVRInput.Controller.RTouch;var secondary=Session.LeftHanded?OVRInput.Controller.RTouch:OVRInput.Controller.LTouch;
                ray=Quest.PointerRay(Session.LeftHanded);down=OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger,primary);up=OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger,primary);
                add=OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger,secondary);order=OVRInput.GetDown(OVRInput.Button.One,primary);cancel=OVRInput.GetDown(OVRInput.Button.Two,primary);
                primaryStick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,primary);secondaryStick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,secondary);
                menuDepth=primaryStick.y;
                if(OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick,secondary))Panel.Recall();
                toggleMenu=OVRInput.GetDown(OVRInput.Button.Start);
                if(Time.unscaledTime>hapticEnd)OVRInput.SetControllerVibration(0,0,primary);
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
            {ClearPointer();if(cancel||toggleMenu)Quest.CancelPlacement();else Quest.PlaceRay(ray,secondaryStick,primaryStick,down);return;}
            // Back/pause must work even when the pointer is over a menu button.
            if(cancel)
            {if(Panel.AcceptsGameInput&&(Session.BuildDefinition!=null||Session.AttackMoveMode))Session.CancelMode();else Panel.Back();ClearPointer();return;}
            if(toggleMenu){Panel.ToggleMenu();ClearPointer();return;}
            if(Panel.Point(ray,down,up,menuDepth)){ClearPointer();return;}
            // Quest keeps the confirmed board view fixed. Navigation remains a desktop
            // development control; sticks only affect placement or a grabbed menu.
            if(!Quest.Active&&(Panel.AcceptsGameInput||Panel.Page==CommandPanel.MenuPage.MatchSetup))
                Board.Navigate(pan,zoom,zoom!=0?1:Time.unscaledDeltaTime);
            if(!Panel.AcceptsGameInput){ClearPointer();return;}
            PointBoard(ray,down,up,order,add);
        }
        private void PointBoard(Ray ray,bool down,bool up,bool order,bool add)
        {
            bool onMap=Board.RayToMap(ray,out var point,out var worldHit);
            Board.Pointer=onMap?point:(Point?)null;Board.Hovered=onMap?Board.EntityAt(point):0;
            // The board plane extends beyond its edges for selection only.
            if(!onMap&&!Board.RayToSelectionPlane(ray,out point)){ClearPointer();return;}
            if(down)
            {
                if(Session.BuildDefinition!=null||Session.AttackMoveMode)
                {if(onMap)Session.Context(point,Board.Hovered,Board.ResourceAt(point));}
                else{start=point;startedOnMap=onMap;dragging=true;}
            }
            if(Quest.Active&&laser!=null&&(onMap||dragging))
                laser.SetPosition(1,onMap?worldHit:Board.Content.TransformPoint(new Vector3(point.x,0,point.y)));
            bool area=start.Distance(point)>1.5f||!startedOnMap||!onMap;
            if(dragging)Board.DrawSelection(start,point,area);
            if(up&&dragging)
            {
                if(area)
                {if(Board.TrySelectionBounds(start,point,out var min,out var max))Session.SelectRectangle(min,max,add);}
                else if(onMap&&startedOnMap)Session.Select(Board.Hovered,add,Board.ResourceAt(point));
                dragging=false;Board.DrawSelection(start,point,false);
            }
            if(order&&onMap)Session.Context(point,Board.Hovered,Board.ResourceAt(point));
        }
        private void Haptic(bool success)
        {if(!Quest.Active)return;OVRInput.SetControllerVibration(.5f,success ? .2f : .65f,Session.LeftHanded?OVRInput.Controller.LTouch:OVRInput.Controller.RTouch);hapticEnd=Time.unscaledTime+.07f;}
        private void OnApplicationFocus(bool focused)
        {if(!focused)ClearPointer();}
        private void OnDisable()=>ClearPointer();
        private void ClearPointer()
        {dragging=false;if(Board!=null){Board.Hovered=0;Board.Pointer=null;Board.DrawSelection(start,start,false);}}
    }
}
