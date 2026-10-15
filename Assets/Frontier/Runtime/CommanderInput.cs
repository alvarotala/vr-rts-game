using Frontier.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frontier.Runtime
{
    public sealed class CommanderInput:MonoBehaviour
    {
        public GameSession Session;public BoardView Board;public CommandPanel Panel;public QuestBridge Quest;
        private Point start;private bool dragging;private LineRenderer laser;private float hapticEnd;
        private void Start()
        {
            Panel.PlaceRequested=Quest.BeginPlacement;Session.Feedback+=Haptic;
            var go=new GameObject("Pointer ray");laser=go.AddComponent<LineRenderer>();laser.sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));laser.sharedMaterial.color=new Color(.3f,.95f,.94f);laser.positionCount=2;laser.widthMultiplier=.002f;laser.enabled=Quest.Active;
        }
        private void Update()
        {
            if(Board.Content==null)return;
            bool down=false,up=false,order=false,add=false,cancel=false;Vector2 pan=Vector2.zero;float zoom=0;Ray ray;
            Vector2 primaryStick=Vector2.zero,secondaryStick=Vector2.zero;
            if(Quest.Active)
            {
                var primary=Session.LeftHanded?OVRInput.Controller.LTouch:OVRInput.Controller.RTouch;var secondary=Session.LeftHanded?OVRInput.Controller.RTouch:OVRInput.Controller.LTouch;
                ray=Quest.PointerRay(Session.LeftHanded);down=OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger,primary);up=OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger,primary);
                add=OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger,secondary);order=OVRInput.GetDown(OVRInput.Button.One,primary);cancel=OVRInput.GetDown(OVRInput.Button.Two,primary);
                primaryStick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,primary);secondaryStick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,secondary);
                pan=secondaryStick;zoom=primaryStick.y;
                if(OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick,primary))Board.Fit();
                if(OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick,secondary))Panel.Recall();
                if(OVRInput.GetDown(OVRInput.Button.Start))Panel.ToggleMenu();
                if(Time.unscaledTime>hapticEnd)OVRInput.SetControllerVibration(0,0,primary);
                laser.SetPosition(0,ray.origin);laser.SetPosition(1,ray.GetPoint(2));
            }
            else
            {
                if(Mouse.current==null)return;ray=Quest.Camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                down=Mouse.current.leftButton.wasPressedThisFrame;up=Mouse.current.leftButton.wasReleasedThisFrame;order=Mouse.current.rightButton.wasPressedThisFrame;
                zoom=Mouse.current.scroll.ReadValue().y*.04f;
                if(Keyboard.current!=null)
                {
                    var k=Keyboard.current;add=k.leftShiftKey.isPressed||k.rightShiftKey.isPressed;cancel=k.escapeKey.wasPressedThisFrame;
                    pan=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                    if(k.spaceKey.wasPressedThisFrame)Session.TogglePause();if(k.fKey.wasPressedThisFrame)Board.Fit();
                    if(k.mKey.wasPressedThisFrame)Panel.Recall();
                    if(k.digit1Key.wasPressedThisFrame)Session.Group(0,k.leftCtrlKey.isPressed);if(k.digit2Key.wasPressedThisFrame)Session.Group(1,k.leftCtrlKey.isPressed);if(k.digit3Key.wasPressedThisFrame)Session.Group(2,k.leftCtrlKey.isPressed);
                }
            }
            if(Quest.Placing){Quest.PlaceRay(ray,secondaryStick,primaryStick,down);return;}
            if(Panel.Point(ray,down,up,zoom)){Board.Hovered=0;Board.Pointer=null;dragging=false;Board.DrawSelection(start,start,false);return;}
            Board.Navigate(pan,zoom,Quest.Active?Time.unscaledDeltaTime:zoom!=0?1:Time.unscaledDeltaTime);
            if(cancel){if(Session.BuildDefinition!=null||Session.AttackMoveMode)Session.CancelMode();else Panel.ToggleMenu();}
            if(!Board.RayToMap(ray,out var point)){Board.Hovered=0;Board.Pointer=null;if(up){dragging=false;Board.DrawSelection(start,start,false);}return;}
            Board.Pointer=point;Board.Hovered=Board.EntityAt(point);
            if(Quest.Active)laser.SetPosition(1,Board.Content.TransformPoint(new Vector3(point.x,Board.HeightAt(point),point.y)));
            if(down)
            {
                if(Session.BuildDefinition!=null||Session.AttackMoveMode)Session.Context(point,Board.Hovered,Board.ResourceAt(point));
                else{start=point;dragging=true;}
            }
            if(dragging)Board.DrawSelection(start,point,start.Distance(point)>1.5f);
            if(up&&dragging)
            {if(start.Distance(point)>1.5f)Session.SelectRectangle(start,point,add);else Session.Select(Board.Hovered,add);dragging=false;Board.DrawSelection(start,point,false);}
            if(order)Session.Context(point,Board.Hovered,Board.ResourceAt(point));
        }
        private void Haptic(bool success)
        {if(!Quest.Active)return;OVRInput.SetControllerVibration(.5f,success ? .2f : .65f,Session.LeftHanded?OVRInput.Controller.LTouch:OVRInput.Controller.RTouch);hapticEnd=Time.unscaledTime+.07f;}
        private void OnApplicationFocus(bool focused)
        {if(!focused){dragging=false;if(Board!=null)Board.DrawSelection(start,start,false);}}
    }
}
