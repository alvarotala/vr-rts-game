#if FRONTIER_BROWSER
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frontier.Runtime
{
    // This is touchscreen input. BrowserTouchInput remains the inert Meta Touch
    // controller adapter used by the shared desktop/mobile runtime.
    public static class BrowserMobileInput
    {
        private static bool usedTouch;
        private static Vector2 lastPosition;
        private static readonly MobileTouchRouter touchRouter=new MobileTouchRouter();
        private static readonly System.Collections.Generic.List<MobileTouchRouter.Contact> contacts=new System.Collections.Generic.List<MobileTouchRouter.Contact>(10);
        private static int pointerFrame=-1;
        private static bool pointerHeld;
        public static RectTransform ShiftButton { get;set; }
        public static bool ShiftActive { get=>touchRouter.ShiftActive;set=>touchRouter.ShiftActive=value; }
        public static MobileTouchRouter.ShiftMode ShiftMode=>touchRouter.Mode;
        public static void ResetShift()=>touchRouter.ResetShift();
        public static bool IsTouchMode
        {
            get
            {
#if FRONTIER_MOBILE
                return true;
#else
                var touch=Touchscreen.current?.primaryTouch;
                if(touch!=null&&(touch.press.isPressed||touch.press.wasReleasedThisFrame))usedTouch=true;
                else if(Mouse.current!=null&&(Mouse.current.leftButton.wasPressedThisFrame||Mouse.current.rightButton.wasPressedThisFrame))usedTouch=false;
                return Application.isMobilePlatform||usedTouch;
#endif
            }
        }
        public static bool HasPointer=>IsTouchMode?Touchscreen.current!=null:Mouse.current!=null;
        public static bool PointerHeld=>IsTouchMode?(pointerFrame==Time.frameCount?pointerHeld:Touchscreen.current?.primaryTouch.press.isPressed==true):Mouse.current?.leftButton.isPressed==true;
        public static Vector2 PointerPosition
        {
            get
            {
                if(!IsTouchMode)return Mouse.current?.position.ReadValue()??lastPosition;
                if(pointerFrame==Time.frameCount)return lastPosition;
                var touch=Touchscreen.current?.primaryTouch;
                if(touch!=null&&(touch.press.isPressed||touch.press.wasReleasedThisFrame))lastPosition=touch.position.ReadValue();
                return lastPosition;
            }
        }
        public static MobileGesture.Sample Read(bool allowShift,bool allowQueue=true)
        {
            var screen=Touchscreen.current;
            bool shiftAvailable=allowShift&&ShiftButton!=null&&ShiftButton.gameObject.activeInHierarchy;
            contacts.Clear();
            if(screen!=null)foreach(var touch in screen.touches)
            {
                bool held=touch.press.isPressed,down=touch.press.wasPressedThisFrame,up=touch.press.wasReleasedThisFrame;
                if(!held&&!down&&!up)continue;
                var point=touch.position.ReadValue();
                contacts.Add(new MobileTouchRouter.Contact{Id=touch.touchId.ReadValue(),X=point.x,Y=point.y,Held=held,Down=down,Up=up,
                    Cancelled=touch.phase.ReadValue()==UnityEngine.InputSystem.TouchPhase.Canceled,
                    OnShift=shiftAvailable&&RectTransformUtility.RectangleContainsScreenPoint(ShiftButton,point,null)});
            }
            var sample=touchRouter.Read(contacts,Time.unscaledTime,shiftAvailable,allowQueue);
            pointerFrame=Time.frameCount;pointerHeld=sample.Count>0;lastPosition=new Vector2(sample.X,sample.Y);
            return sample;
        }
    }
}
#endif
