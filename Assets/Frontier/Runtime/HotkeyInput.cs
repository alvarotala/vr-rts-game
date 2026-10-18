using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Frontier.Runtime
{
    // Reads the existing Input System / OVR devices without replacing their lifecycle.
    public static class HotkeyInput
    {
        public readonly struct Signal
        {
            public readonly bool Down,Up,Held;
            public Signal(bool down,bool up,bool held){Down=down;Up=up;Held=held;}
        }
        public static Signal Read(HotkeyBindings bindings,bool touch,string id)=>Read(bindings.Get(touch,id),bindings.TouchController);
        public static Signal Read(string input,OVRInput.Controller controller=OVRInput.Controller.None)
        {
            if(input.StartsWith("touch:"))
            {
                // Never fall back to OVR's "Active"/combined controller masks: only the
                // hand explicitly chosen by the player may drive an input.
                if(controller!=OVRInput.Controller.LTouch&&controller!=OVRInput.Controller.RTouch)return default;
                var button=TouchButton(input);
                return new Signal(OVRInput.GetDown(button,controller),OVRInput.GetUp(button,controller),OVRInput.Get(button,controller));
            }
            ButtonControl control=null,other=null;
            if(input.StartsWith("mouse:")&&Mouse.current!=null)
            {
                var mouse=Mouse.current;
                switch(input){case "mouse:left":control=mouse.leftButton;break;case "mouse:right":control=mouse.rightButton;break;case "mouse:middle":control=mouse.middleButton;break;case "mouse:forward":control=mouse.forwardButton;break;case "mouse:back":control=mouse.backButton;break;}
            }
            else if(input.StartsWith("key:")&&Keyboard.current!=null&&System.Enum.TryParse(input.Substring(4),out Key key))
            {
                control=Keyboard.current[key];
                if(key==Key.LeftShift)other=Keyboard.current.rightShiftKey;
                if(key==Key.LeftCtrl)other=Keyboard.current.rightCtrlKey;
                if(key==Key.LeftAlt)other=Keyboard.current.rightAltKey;
            }
            bool held=control?.isPressed==true||other?.isPressed==true;
            return new Signal(control?.wasPressedThisFrame==true||other?.wasPressedThisFrame==true,
                !held&&(control?.wasReleasedThisFrame==true||other?.wasReleasedThisFrame==true),held);
        }
        private static OVRInput.Button TouchButton(string input)
        {
            switch(input){case "touch:trigger":return OVRInput.Button.PrimaryIndexTrigger;case "touch:grip":return OVRInput.Button.PrimaryHandTrigger;case "touch:a":return OVRInput.Button.One;case "touch:b":return OVRInput.Button.Two;default:return OVRInput.Button.PrimaryThumbstick;}
        }
        public static bool AnyHeld(bool touch,OVRInput.Controller controller=OVRInput.Controller.None)
        {
            if(touch){foreach(var d in HotkeyBindings.Definitions(true))if(Read(d.Default,controller).Held)return true;return false;}
            if(Keyboard.current!=null)foreach(var key in Keyboard.current.allKeys)if(key.isPressed)return true;
            foreach(string input in MouseButtons)if(Read(input).Held)return true;return false;
        }
        private static readonly string[] MouseButtons={"mouse:left","mouse:right","mouse:middle","mouse:forward","mouse:back"};
        public static string Pressed(bool touch,OVRInput.Controller controller=OVRInput.Controller.None)
        {
            if(touch){foreach(var d in HotkeyBindings.Definitions(true))if(Read(d.Default,controller).Down)return d.Default;return null;}
            if(Keyboard.current!=null)foreach(var key in Keyboard.current.allKeys)
            {
                if(!key.wasPressedThisFrame)continue;
                var code=key.keyCode;
                if(code==Key.RightShift)code=Key.LeftShift;if(code==Key.RightCtrl)code=Key.LeftCtrl;if(code==Key.RightAlt)code=Key.LeftAlt;
                return "key:"+code;
            }
            foreach(string input in MouseButtons)if(Read(input).Down)return input;return null;
        }
    }
}
