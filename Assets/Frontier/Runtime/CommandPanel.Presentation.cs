using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private Text consoleBrand,consoleBrandSmall;
        private RectTransform moveAffordance;
        private Image moveAffordanceImage;
        private Text moveCaption,moveHints;

        private void CreateConsoleChrome()
        {
            consoleBrandSmall=Label(root,"LITTLE",new Vector2(32,-20),new Vector2(280,24),18,ConsoleTheme.Muted);consoleBrandSmall.fontStyle=FontStyle.Bold;
            consoleBrand=Label(root,"LEGION",new Vector2(32,-40),new Vector2(330,50),38,ConsoleTheme.Ink);consoleBrand.fontStyle=FontStyle.Bold;
            moveAffordance=Section(moveHandle,"Move affordance",Vector2.zero,new Vector2(192,12),ConsoleTheme.Muted);
            moveAffordanceImage=moveAffordance.GetComponent<Image>();
            moveCaption=Label(moveAffordance,"Move window",Vector2.zero,new Vector2(232,52),23,ConsoleTheme.HoverInk,TextAnchor.MiddleCenter);
            moveCaption.fontStyle=FontStyle.Bold;
            moveHints=Label(moveHandle,"",new Vector2(-290,-88),new Vector2(900,64),20,ConsoleTheme.Ink,TextAnchor.UpperCenter);
            moveHints.gameObject.name="Move window hints";
        }

        private void LayoutConsoleChrome()
        {
            bool hud=Page==MenuPage.Actions,main=Page==MenuPage.Main;
            consoleBrand.gameObject.SetActive(main);consoleBrandSmall.gameObject.SetActive(main);
            resetHandle.gameObject.SetActive(false);
            moveAffordance.gameObject.SetActive(true);
            // The same detached grip and hit area belong to every page.
            TopLeft(moveHandle,new Vector2((root.rect.width-320)*.5f,-root.rect.height-8),new Vector2(320,80));
            moveHandle.GetComponentInChildren<Text>(true).gameObject.SetActive(false);
            UpdateMoveAffordance(moving);
            selection.gameObject.SetActive(!main);
            if(hud)return;

            headline.gameObject.SetActive(!main);
            headline.fontSize=36;headline.fontStyle=FontStyle.Bold;headline.color=ConsoleTheme.Ink;
            headline.alignment=TextAnchor.MiddleLeft;
            TopLeft(headline.rectTransform,new Vector2(30,-32),new Vector2(1040,48));
            economy.fontSize=18;economy.color=ConsoleTheme.Muted;economy.alignment=TextAnchor.MiddleLeft;
            TopLeft(economy.rectTransform,new Vector2(30,-88),new Vector2(1040,28));
            selection.gameObject.SetActive(!main);selection.fontSize=20;selection.color=ConsoleTheme.Muted;
            TopLeft(selection.rectTransform,new Vector2(30,-132),new Vector2(1040,48));
            TopLeft(harvesting.rectTransform,new Vector2(30,-196),new Vector2(1040,28));
            if(!main)return;

            // Flat, large wordmark. It is the only branding in the interface.
            consoleBrandSmall.fontSize=72;consoleBrand.fontSize=72;
            consoleBrandSmall.color=ConsoleTheme.Ink;consoleBrand.color=ConsoleTheme.Accent;
            float littleWidth=consoleBrandSmall.preferredWidth,legionWidth=consoleBrand.preferredWidth;
            float left=(root.rect.width-littleWidth-legionWidth-20)*.5f;
            TopLeft(consoleBrandSmall.rectTransform,new Vector2(left,-40),new Vector2(littleWidth+2,92));
            TopLeft(consoleBrand.rectTransform,new Vector2(left+littleWidth+20,-40),new Vector2(legionWidth+2,92));
            economy.fontSize=24;economy.color=ConsoleTheme.Ink;economy.alignment=TextAnchor.MiddleCenter;
            TopLeft(economy.rectTransform,new Vector2(30,-144),new Vector2(1040,36));
            TopLeft(actions,new Vector2(30,-260),new Vector2(1040,452));
        }
        private void UpdateMoveAffordance(bool focused)
        {
            if(moveAffordance==null)return;
            moveImage.color=Color.clear;
            var size=focused?new Vector2(264,52):new Vector2(192,12);
            TopLeft(moveAffordance,new Vector2((320-size.x)*.5f,-(80-size.y)*.5f),size);
            moveAffordanceImage.color=focused?ConsoleTheme.Accent:ConsoleTheme.Muted;
            ConsoleTheme.StylePill(moveAffordanceImage);
            moveCaption.gameObject.SetActive(focused);
            TopLeft(moveCaption.rectTransform,new Vector2(16,0),new Vector2(size.x-32,size.y));
            moveHints.gameObject.SetActive(focused);
            if(focused)
            {
                if(Quest.Active)((LocalizedText)moveHints).SetKey("Hold Trigger / {0} and move\nStick ← → Size · ↑ ↓ Distance · Release to place",Bindings.MenuConfirmName);
                else moveHints.text="Hold left mouse and drag\nMouse wheel · Distance · Release to place";
            }
        }
        private ConsoleGlyph Glyph(RectTransform parent,ConsoleGlyph.Kind kind,Vector2 position,float size,Color color)
        {
            var go=new GameObject(kind+" icon",typeof(RectTransform),typeof(ConsoleGlyph));
            var glyph=go.GetComponent<ConsoleGlyph>();glyph.transform.SetParent(parent,false);TopLeft(glyph.rectTransform,position,new Vector2(size,size));
            glyph.Glyph=kind;glyph.color=color;return glyph;
        }
    }
}
