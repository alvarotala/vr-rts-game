using UnityEngine;
using TMPro;

namespace Frontier.Runtime
{
    // Keep the original binding, including composed messages. Translation never changes
    // GameObject names, command IDs or the strings used for gameplay comparisons.
    public sealed class LocalizedText : TextMeshProUGUI
    {
        private string source="";
        private object[] arguments;
        public string Source=>arguments==null?source:string.Format(System.Globalization.CultureInfo.InvariantCulture,source,arguments);
        public void Append(string suffix){text=Source+suffix;}
        public void SetKey(string key,params object[] values){source=key;arguments=values;RefreshLanguage();}
        public override string text
        {
            get=>base.text;
            set{value=value??"";if(source==value&&arguments==null)return;source=value;arguments=null;RefreshLanguage();}
        }
        protected override void Awake()
        {
            font = UiTypography.Font;
            base.Awake();
            textWrappingMode = TextWrappingModes.Normal;
            overflowMode = TextOverflowModes.Truncate;
            raycastTarget = false;
        }
        protected override void OnEnable(){base.OnEnable();Localization.Changed+=RefreshLanguage;RefreshLanguage();}
        protected override void OnDisable(){Localization.Changed-=RefreshLanguage;base.OnDisable();}
        private void RefreshLanguage(){base.text=arguments==null?Localization.Text(source):Localization.Format(source,arguments);}
    }
}
