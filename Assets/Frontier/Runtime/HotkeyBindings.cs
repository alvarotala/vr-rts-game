using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frontier.Runtime
{
    // Logical inputs own their tap/hold/double-tap gestures together. Menu navigation
    // stays fixed so a gameplay remap can never lock the player out of this editor.
    public sealed class HotkeyBindings
    {
        public const string PreferenceKey="frontier.hotkeys.v1";
        public bool LeftHand { get; private set; }
        public OVRInput.Controller TouchController=>LeftHand?OVRInput.Controller.LTouch:OVRInput.Controller.RTouch;
        public string HandName=>LeftHand?"Left":"Right";
        public string OtherHandName=>LeftHand?"Right":"Left";
        public string MenuConfirmName=>LeftHand?"X":"A";
        public string MenuBackName=>LeftHand?"Y":"B";
        public void SelectHand(bool left)=>LeftHand=left;
        public sealed class Definition
        {
            public readonly string Id,Name,Default,Description;
            public readonly bool Modifier;
            public Definition(string id,string name,string input,string description,bool modifier=false)
            {Id=id;Name=name;Default=input;Description=description;Modifier=modifier;}
        }
        private static readonly Definition[] Touch={
            new Definition("select","Select / place","touch:trigger","Selection, drag, double-tap selection and board placement."),
            new Definition("order","Context order / confirm","touch:a","Move, attack, gather, repair and rally. Double-tap for Attack Move."),
            new Definition("add","Add / queue modifier","touch:grip","Hold to add selection or queue orders."),
            new Definition("back","Cancel / pause","touch:b","Cancel the current action or open pause."),
            new Definition("wheel","Show / hide hints","touch:stick","Toggle controller hints outside Groups. In Groups, physical Stick click assigns the highlighted group.")
        };
        private static readonly Definition[] Desktop={
            new Definition("select","Select / place","mouse:left","Selection, drag, double-click selection and board placement."),
            new Definition("order","Context order","mouse:right","Move, attack, gather, repair and rally. Double-click for Attack Move."),
            new Definition("add","Add / queue modifier","key:LeftShift","Hold to add selection or queue orders. Both sides of the modifier work.",true),
            new Definition("back","Cancel / pause","key:Escape","Cancel the current action; otherwise open pause."),
            new Definition("pause","Pause / resume shortcut","key:Space","Open pause or resume. Escape always works inside menus."),
            new Definition("save","Replace group modifier","key:LeftCtrl","Hold with a group key to replace its members. Both sides work.",true),
            new Definition("group1","Recall group 1","key:Digit1","Recall group 1; hold the replace modifier to save your selection."),
            new Definition("group2","Recall group 2","key:Digit2","Recall group 2; hold the replace modifier to save your selection."),
            new Definition("group3","Recall group 3","key:Digit3","Recall group 3; hold the replace modifier to save your selection."),
            new Definition("group4","Recall group 4","key:Digit4","Recall group 4; hold the replace modifier to save your selection."),
            new Definition("pan-up","Pan forward","key:W","Hold to move the desktop view forward."),
            new Definition("pan-down","Pan backward","key:S","Hold to move the desktop view backward."),
            new Definition("pan-left","Pan left","key:A","Hold to move the desktop view left."),
            new Definition("pan-right","Pan right","key:D","Hold to move the desktop view right."),
            new Definition("fit","Full map","key:F","Fit the whole battlefield in the desktop view."),
            new Definition("menu","Recover menu","key:M","Return the console above the board.")
        };
        private readonly Dictionary<string,string> touch=new Dictionary<string,string>(),desktop=new Dictionary<string,string>();
        [Serializable] private sealed class Saved { public int version=1; public bool leftHand; public Entry[] entries; }
        [Serializable] private sealed class Entry { public bool touch; public string id,input; }
        public HotkeyBindings(){Reset(true);Reset(false);}
        public static IReadOnlyList<Definition> Definitions(bool quest)=>quest?Array.AsReadOnly(Touch):Array.AsReadOnly(Desktop);
        private Dictionary<string,string> Values(bool quest)=>quest?touch:desktop;
        public string Get(bool quest,string id)=>Values(quest)[id];
        public string Name(bool quest,string id)=>Display(Get(quest,id),LeftHand);
        public void Reset(bool quest){var values=Values(quest);values.Clear();foreach(var d in Definitions(quest))values[d.Id]=d.Default;}
        public string Conflict(bool quest,string id,string input)=>Values(quest).FirstOrDefault(p=>p.Key!=id&&p.Value==input).Key;
        public bool Supports(bool quest,string id,string input)=>Definitions(quest).Any(d=>d.Id==id&&Allowed(quest,d,input));
        public bool Assign(bool quest,string id,string input,bool swap,out string error)
        {
            var definition=Definitions(quest).FirstOrDefault(d=>d.Id==id);
            if(definition==null||!Allowed(quest,definition,input))
            {
                error=quest&&id=="wheel"?"Use Stick click, "+MenuConfirmName+" or Grip. Trigger and "+MenuBackName+" navigate the wheel.":
                    definition?.Modifier==true?"Choose Shift, Ctrl or Alt.":"Choose a key or button, without a modifier combination.";
                return false;
            }
            string other=Conflict(quest,id,input);
            if(other!=null)
            {
                if(!swap){error="Already used by "+Definitions(quest).First(d=>d.Id==other).Name+".";return false;}
                var otherDefinition=Definitions(quest).First(d=>d.Id==other);
                if(!Allowed(quest,otherDefinition,Get(quest,id))){error="These inputs cannot be swapped.";return false;}
                Values(quest)[other]=Get(quest,id);
            }
            Values(quest)[id]=input;error=null;return true;
        }
        private static bool Allowed(bool quest,Definition definition,string input)
        {
            if(string.IsNullOrEmpty(input))return false;
            if(quest)return Touch.Any(d=>d.Default==input)&&(definition.Id!="wheel"||input!="touch:trigger"&&input!="touch:b");
            bool modifier=input=="key:LeftShift"||input=="key:LeftCtrl"||input=="key:LeftAlt";
            if(definition.Modifier)return modifier;
            if(modifier)return false;
            if(input.StartsWith("mouse:",StringComparison.Ordinal))return new[]{"mouse:left","mouse:right","mouse:middle","mouse:forward","mouse:back"}.Contains(input);
            return input.StartsWith("key:",StringComparison.Ordinal)&&Enum.TryParse(input.Substring(4),out Key key)&&Enum.IsDefined(typeof(Key),key)&&input=="key:"+key&&key>Key.None&&key!=Key.IMESelected
                &&key!=Key.RightShift&&key!=Key.RightCtrl&&key!=Key.RightAlt&&key!=Key.LeftMeta&&key!=Key.RightMeta;
        }
        public string ToJson()=>JsonUtility.ToJson(new Saved{leftHand=LeftHand,entries=new[]{false,true}.SelectMany(q=>Values(q).Select(p=>new Entry{touch=q,id=p.Key,input=p.Value})).ToArray()});
        public static HotkeyBindings FromJson(string json)
        {
            var result=new HotkeyBindings();
            try
            {
                var saved=JsonUtility.FromJson<Saved>(json);
                if(saved?.version!=1||saved.entries==null)return result;
                result.LeftHand=saved.leftHand;
                // Validate each complete device profile before installing any of it.
                foreach(bool quest in new[]{false,true})
                {
                    var entries=saved.entries.Where(e=>e!=null&&e.touch==quest).ToArray();var definitions=Definitions(quest);
                    if(entries.Length!=definitions.Count||entries.Select(e=>e.id).Distinct().Count()!=definitions.Count||entries.Select(e=>e.input).Distinct().Count()!=definitions.Count)continue;
                    if(quest&&definitions.All(d=>entries.Any(e=>e.id==d.Id&&Touch.Any(t=>t.Default==e.input))))
                    {
                        // Older profiles allowed Trigger/B to open the wheel. They
                        // now navigate it, so move the hint toggle back to stick click
                        // and preserve every other mapping with one pairwise swap.
                        var save=entries.First(e=>e.id=="wheel");
                        if(save.input=="touch:trigger"||save.input=="touch:b")
                        {
                            var stick=entries.First(e=>e.input=="touch:stick");
                            stick.input=save.input;save.input="touch:stick";
                        }
                    }
                    if(definitions.Any(d=>!entries.Any(e=>e.id==d.Id&&Allowed(quest,d,e.input))))continue;
                    foreach(var entry in entries)result.Values(quest)[entry.id]=entry.input;
                }
            }
            catch(ArgumentException){ /* Keep usable defaults if preferences are corrupt. */ }
            return result;
        }
        public static HotkeyBindings Load()=>FromJson(PlayerPrefs.GetString(PreferenceKey,""));
        public void Save(){PlayerPrefs.SetString(PreferenceKey,ToJson());PlayerPrefs.Save();}
        public static string Display(string input,bool leftHand=false)
        {
            switch(input)
            {
                case "touch:trigger":return "Trigger";case "touch:grip":return "Grip";case "touch:a":return leftHand?"X":"A";case "touch:b":return leftHand?"Y":"B";case "touch:stick":return "Stick click";
                case "mouse:left":return "Left mouse";case "mouse:right":return "Right mouse";case "mouse:middle":return "Middle mouse";case "mouse:forward":return "Mouse forward";case "mouse:back":return "Mouse back";
                case "key:LeftShift":return "Shift";case "key:LeftCtrl":return "Ctrl";case "key:LeftAlt":return "Alt";
            }
            string key=input.Substring(4);return key.StartsWith("Digit",StringComparison.Ordinal)?key.Substring(5):key;
        }
    }
}
