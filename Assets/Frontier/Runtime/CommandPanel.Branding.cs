using System;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private RectTransform brandLogo, headerRule, footerRule, pageEmblem;
        private MenuIcon pageIcon;

        private void CreateBrand()
        {
            var logo=new GameObject("Little Legion logo",typeof(RectTransform),typeof(LegionLogo));
            brandLogo=logo.GetComponent<RectTransform>();brandLogo.SetParent(root,false);
            logo.GetComponent<LegionLogo>().raycastTarget=false;
            headerRule=Section(root,"Header divider",new Vector2(32,-70),new Vector2(1036,1),new Color(.1f,.24f,.3f,.42f));
            footerRule=Section(root,"Footer divider",Vector2.zero,new Vector2(1036,1),new Color(.1f,.24f,.3f,.42f));
            pageEmblem=Section(root,"Page emblem",new Vector2(32,-86),new Vector2(46,46),MenuPalette.Surface);
            pageEmblem.GetComponent<MenuSurface>().Style=MenuSurface.SurfaceStyle.Inset;
            pageEmblem.GetComponent<MenuSurface>().Radius=14;
            pageIcon=Icon(pageEmblem,MenuIcon.Symbol.Settings,new Vector2(10,-10),26,MenuPalette.Accent);
        }

        private void LayoutBrand()
        {
            bool main=Page==MenuPage.Main,hud=Page==MenuPage.Actions;
            TopLeft(brandLogo,main?new Vector2(150,-107):hud?new Vector2(30,-22):new Vector2(820,-94),
                main?new Vector2(800,84):hud?new Vector2(250,26):new Vector2(240,26));
            headline.gameObject.SetActive(!main&&!hud);
            TopLeft(headline.rectTransform,new Vector2(94,-86),new Vector2(700,44));
            headline.fontStyle=FontStyle.Bold;
            headline.color=MenuPalette.Ink;
            pageEmblem.gameObject.SetActive(!main&&!hud);pageIcon.Icon=PageSymbol();
            headerRule.gameObject.SetActive(!hud);
            economy.alignment=main?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft;
            economy.fontSize=main?25:18;economy.color=MenuPalette.Muted;
            TopLeft(economy.rectTransform,main?new Vector2(60,-207):new Vector2(30,-130),main?new Vector2(980,38):new Vector2(1040,36));
            selection.gameObject.SetActive(!main);harvesting.gameObject.SetActive(!main);
            if(main)
            {
                root.sizeDelta=new Vector2(1100,690);
                TopLeft(actions,new Vector2(90,-278),new Vector2(920,316));
                TopLeft(statusBanner,new Vector2(90,-620),new Vector2(920,48));
            }
            TopLeft(moveHandle,hud?new Vector2(306,-12):new Vector2(20,-12),hud?new Vector2(570,44):new Vector2(810,44));
            TopLeft(resetHandle,hud?new Vector2(892,-12):new Vector2(844,-12),hud?new Vector2(188,44):new Vector2(236,44));
            foreach(var handle in new[]{moveHandle,resetHandle})
            {
                var label=handle.GetComponentInChildren<Text>();
                label.fontSize=hud?13:14;
                TopLeft(label.rectTransform,new Vector2(44,0),handle.sizeDelta-new Vector2(56,0));
            }
            TopLeft(footerRule,new Vector2(statusBanner.anchoredPosition.x,statusBanner.anchoredPosition.y+6),new Vector2(statusBanner.sizeDelta.x,1));
        }

        private MenuIcon.Symbol PageSymbol()
        {
            switch(Page)
            {
                case MenuPage.MatchSetup:return MenuIcon.Symbol.Play;
                case MenuPage.Pause:return MenuIcon.Symbol.Pause;
                case MenuPage.OnlineSetup:case MenuPage.OnlineEndpoint:return MenuIcon.Symbol.Network;
                case MenuPage.Guides:case MenuPage.Guide:return MenuIcon.Symbol.Book;
                case MenuPage.UnitsGuide:return MenuIcon.Symbol.Army;
                case MenuPage.StructuresGuide:return MenuIcon.Symbol.Build;
                case MenuPage.Hotkeys:return MenuIcon.Symbol.Controls;
                case MenuPage.History:case MenuPage.Result:case MenuPage.HistoryReport:return MenuIcon.Symbol.History;
                case MenuPage.Replay:return MenuIcon.Symbol.Play;
                case MenuPage.LeaveConfirmation:return MenuIcon.Symbol.Home;
                default:return MenuIcon.Symbol.Settings;
            }
        }

        private void StyleMenuTargets()
        {
            foreach(var target in targets)
            {
                if(target?.rect==null||target.image==null)continue;
                if(!(target.image is MenuSurface surface))continue;
                surface.Style=MenuSurface.SurfaceStyle.Button;
                surface.Radius=Mathf.Min(12,target.rect.rect.height*.2f);
                if(target.help==null)surface.BorderColor=target.normal==MenuPalette.Primary?accent:MenuPalette.Border;
                surface.State=0;
            }
        }
        private void UpdateMenuFeedback(float delta)
        {
            float step=Mathf.Clamp(delta,0,.05f)*10;
            foreach(var target in targets)
                if(target?.rect!=null&&target.image!=null&&target.image is MenuSurface surface)
                {
                    if(target.help==null)surface.BorderColor=target.normal==MenuPalette.Primary?accent:MenuPalette.Border;
                    surface.FocusColor=target.selectionType!=null&&selectionRemove?ErrorTint:target.hoverColor??accent;
                    surface.State=Mathf.MoveTowards(surface.State,target==hovered?1:0,step);
                    if(!target.hoverColor.HasValue&&target.selectionType==null)
                        surface.color=Color.Lerp(target.normal,accent,surface.State*.12f);
                }
        }
        private static void ResetTargetFeedback(Target target,bool immediate=true)
        {
            if(target?.image==null)return;
            if(immediate||target.hoverColor.HasValue||target.selectionType!=null)target.image.color=target.normal;
            if(immediate&&target.image is MenuSurface surface)surface.State=0;
        }
        private static void SetHandleFeedback(Image image,bool active)
        {
            image.color=active?new Color(.03f,.14f,.17f,.75f):new Color(.02f,.05f,.07f,.18f);
        }

        private MenuIcon Icon(RectTransform parent,MenuIcon.Symbol symbol,Vector2 position,float size,Color color)
        {
            var icon=new GameObject(symbol+" icon",typeof(RectTransform),typeof(MenuIcon)).GetComponent<MenuIcon>();
            icon.transform.SetParent(parent,false);TopLeft(icon.rectTransform,position,new Vector2(size,size));
            icon.Icon=symbol;icon.color=color;icon.raycastTarget=false;return icon;
        }

        private void NavigationCard(string title,MenuIcon.Symbol symbol,Vector2 position,Vector2 size,Action click,bool primary=false)
        {
            var rect=Section(actions,title,position,size,primary?MenuPalette.Primary:button);
            var surface=rect.GetComponent<MenuSurface>();surface.Radius=14;surface.BorderWidth=1.5f;
            if(primary)surface.BorderColor=accent;
            float iconSize=primary?44:40;
            Icon(rect,symbol,new Vector2(30,-(size.y-iconSize)*.5f),iconSize,MenuPalette.Ink);
            var titleLabel=Label(rect,title,new Vector2(98,0),new Vector2(size.x-154,size.y),primary?32:24,MenuPalette.Ink);
            titleLabel.fontStyle=primary?FontStyle.Bold:FontStyle.Normal;
            Icon(rect,MenuIcon.Symbol.Chevron,new Vector2(size.x-48,-(size.y-24)*.5f),24,primary?new Color(.45f,.9f,.3f):accent);
            targets.Add(new Target{rect=rect,image=surface,normal=surface.color,click=click});
        }
    }
}
