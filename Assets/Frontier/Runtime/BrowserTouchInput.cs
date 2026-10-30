#if FRONTIER_BROWSER
using UnityEngine;

namespace Frontier.Runtime
{
    // Keeps the shared input/UI API available without loading Meta XR in the browser.
    // Keyboard and mouse still use HotkeyInput and the Unity Input System directly.
    public static class BrowserTouchInput
    {
        public enum Controller { None, LTouch, RTouch }
        public enum Button { PrimaryIndexTrigger, PrimaryHandTrigger, One, Two, PrimaryThumbstick, Start }
        public enum Axis1D { PrimaryIndexTrigger, PrimaryHandTrigger }
        public enum Axis2D { PrimaryThumbstick }

        public static bool Get(Button button,Controller controller=Controller.None)=>false;
        public static bool GetDown(Button button,Controller controller=Controller.None)=>false;
        public static bool GetUp(Button button,Controller controller=Controller.None)=>false;
        public static float Get(Axis1D axis,Controller controller=Controller.None)=>0;
        public static Vector2 Get(Axis2D axis,Controller controller=Controller.None)=>Vector2.zero;
        public static void SetControllerVibration(float frequency,float amplitude,Controller controller=Controller.None) { }
    }
}
#endif
