using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private void ApplyTargetHover(Target target,bool active,Color? resting=null)
        {
            if(target==null||target.image==null)return;
            target.image.color=active?ConsoleTheme.Accent:resting??target.normal;
            if(active)
            {
                if(target.hoverInk==null)target.hoverInk=new Dictionary<Graphic,Color>();
                foreach(var graphic in target.rect.GetComponentsInChildren<Graphic>(true))
                    if(graphic is Text||graphic is ConsoleGlyph)Highlight(graphic);
                // A hotkey's input label sits above its clickable sibling surface.
                if(target.hoverExtra!=null)Highlight(target.hoverExtra);
            }
            else if(target.hoverInk!=null)
            {
                foreach(var saved in target.hoverInk)
                    if(saved.Key!=null&&saved.Key.color==ConsoleTheme.HoverInk)saved.Key.color=saved.Value;
                target.hoverInk.Clear();
            }

            void Highlight(Graphic graphic)
            {
                if(!target.hoverInk.ContainsKey(graphic)||graphic.color!=ConsoleTheme.HoverInk)
                    target.hoverInk[graphic]=graphic.color;
                graphic.color=ConsoleTheme.HoverInk;
            }
        }
    }
}
