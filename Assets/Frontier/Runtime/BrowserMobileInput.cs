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
        public static bool AreaSelection { get; set; }
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
        public static bool PointerHeld=>IsTouchMode?Touchscreen.current?.primaryTouch.press.isPressed==true:Mouse.current?.leftButton.isPressed==true;
        public static Vector2 PointerPosition
        {
            get
            {
                if(!IsTouchMode)return Mouse.current?.position.ReadValue()??lastPosition;
                var touch=Touchscreen.current?.primaryTouch;
                if(touch!=null&&(touch.press.isPressed||touch.press.wasReleasedThisFrame))lastPosition=touch.position.ReadValue();
                return lastPosition;
            }
        }
        public static MobileGesture.Sample Read()
        {
            var screen=Touchscreen.current;if(screen==null)return default;
            var primary=screen.primaryTouch;var position=PointerPosition;
            var sample=new MobileGesture.Sample{X=position.x,Y=position.y,Time=Time.unscaledTime,
                Down=primary.press.wasPressedThisFrame,Up=primary.press.wasReleasedThisFrame,
                Cancelled=primary.phase.ReadValue()==UnityEngine.InputSystem.TouchPhase.Canceled};
            foreach(var touch in screen.touches)
            {
                if(!touch.press.isPressed)continue;
                sample.Count++;
                var point=touch.position.ReadValue();
                if(sample.Count==1){sample.Finger=touch.touchId.ReadValue();sample.X=point.x;sample.Y=point.y;}
                else if(sample.Count==2){sample.OtherFinger=touch.touchId.ReadValue();sample.OtherX=point.x;sample.OtherY=point.y;}
            }
            return sample;
        }
    }
}
#endif
