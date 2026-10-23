using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const float ActionHelpDelay=1.5f;
        private float hoverStartedAt;
        private RectTransform actionHelp,sectionHelpBackdrop;
        private Target sectionHelpTarget,sectionHelpClose;
        private bool sectionHelpClosePressed;
        public bool SectionHelpOpen=>sectionHelpTarget!=null;
        private Text helpCategory,helpTitle,helpDescription,helpDetail;
        private readonly List<Target> hudHelpTargets=new List<Target>();

        private void CreateActionHelp()
        {
            sectionHelpBackdrop=Section(root,"Section help backdrop",Vector2.zero,root.sizeDelta,new Color(0,0,0,.62f));
            sectionHelpBackdrop.gameObject.SetActive(false);
            // Action hover replaces the first selection row. Section guides use
            // the same readable text in a centered dialog, with explicit dismissal.
            actionHelp=Section(root,"Action help",new Vector2(32,-HudBodyTop),new Vector2(HudInnerWidth,HudSelectionHeight),ConsoleTheme.Card);
            helpCategory=Label(actionHelp,"",new Vector2(24,-12),new Vector2(HudInnerWidth-48,24),16,accent);
            helpTitle=Label(actionHelp,"",new Vector2(24,-48),new Vector2(280,88),28,ConsoleTheme.Ink);
            helpTitle.fontStyle=FontStyle.Bold;
            helpDescription=Label(actionHelp,"",new Vector2(340,-44),new Vector2(HudInnerWidth-364,76),20,ConsoleTheme.Muted,TextAnchor.UpperLeft);
            helpDetail=Label(actionHelp,"",new Vector2(340,-124),new Vector2(HudInnerWidth-364,44),18,accent,TextAnchor.UpperLeft);
            sectionHelpClose=HudButton(actionHelp,"Close help","",new Vector2(768,-524),new Vector2(240,64),CloseSectionHelp,null,out var closeDetail);
            closeDetail.gameObject.SetActive(false);
            sectionHelpClose.normal=ConsoleTheme.Primary;sectionHelpClose.image.color=sectionHelpClose.normal;
            var closeLabel=sectionHelpClose.rect.GetComponentInChildren<Text>();closeLabel.text="Close";closeLabel.fontSize=24;
            TopLeft(closeLabel.rectTransform,Vector2.zero,new Vector2(240,64));
            sectionHelpClose.rect.gameObject.SetActive(false);
            actionHelp.gameObject.SetActive(false);
        }
        private Target HudHelpButton(RectTransform parent,string name,Vector2 position,string title,Func<string> hint)
        {
            var rect=Section(parent,name,position,new Vector2(44,44),ConsoleTheme.Card);
            var label=Label(rect,"?",Vector2.zero,new Vector2(44,44),22,ConsoleTheme.Ink,TextAnchor.MiddleCenter);
            label.fontStyle=FontStyle.Bold;
            var target=new Target { rect=rect,image=rect.GetComponent<Image>(),normal=ConsoleTheme.Card,hintTitle=title,hint=hint };
            target.click=()=>OpenSectionHelp(target);
            hudHelpTargets.Add(target);
            return target;
        }
        private void BindHudHelpTargets()
        {
            hudHelpTargets.RemoveAll(target=>target.rect==null);
            foreach(var target in hudHelpTargets)
                if(target.rect.gameObject.activeInHierarchy&&!targets.Contains(target))targets.Add(target);
        }
        private void HideActionHelp()
        {
            if(!SectionHelpOpen&&actionHelp!=null)actionHelp.gameObject.SetActive(false);
            hoverStartedAt=Time.unscaledTime;
        }
        private void OpenSectionHelp(Target target)
        {
            if(!AcceptsGameInput||Quest.Placing||target.hint==null)return;
            sectionHelpTarget=target;sectionHelpClosePressed=false;pressed=null;
            UpdateMoveAffordance(false);
            sectionHelpBackdrop.SetAsLastSibling();actionHelp.SetAsLastSibling();
            UpdateActionHelp();
        }
        public void CloseSectionHelp()
        {
            bool wasOpen=SectionHelpOpen;
            sectionHelpTarget=null;sectionHelpClosePressed=false;
            ApplyTargetHover(sectionHelpClose,false);
            if(sectionHelpBackdrop!=null)sectionHelpBackdrop.gameObject.SetActive(false);
            if(sectionHelpClose!=null&&sectionHelpClose.rect!=null)sectionHelpClose.rect.gameObject.SetActive(false);
            if(!wasOpen)return;
            ApplyTargetHover(hovered,false);
            hovered=null;pressed=null;HideActionHelp();
        }
        private bool PointSectionHelp(Ray ray,bool down,bool up)
        {
            if(!SectionHelpOpen)return false;
            UpdateActionHelp();
            if(!SectionHelpOpen)return true;
            bool close=Hit(sectionHelpClose.rect,ray,out _),outside=!Hit(actionHelp,ray,out _);
            ApplyTargetHover(sectionHelpClose,close);
            if(down)sectionHelpClosePressed=close||outside;
            if(up)
            {
                bool dismiss=sectionHelpClosePressed&&(close||outside);
                sectionHelpClosePressed=false;
                if(dismiss)CloseSectionHelp();
            }
            // A click on the backdrop still belongs to the dialog through its
            // release; it can never select a unit or send an order underneath.
            return true;
        }
        private void UpdateActionHelp()
        {
            if(actionHelp==null)return;
            if(SectionHelpOpen)
            {
                if(!AcceptsGameInput||Quest.Placing||moving||!sectionHelpTarget.rect.gameObject.activeInHierarchy)
                {CloseSectionHelp();return;}
                const float width=1040,height=620;
                TopLeft(sectionHelpBackdrop,Vector2.zero,root.sizeDelta);
                TopLeft(actionHelp,new Vector2((root.rect.width-width)*.5f,-(root.rect.height-height)*.5f),new Vector2(width,height));
                sectionHelpBackdrop.gameObject.SetActive(true);actionHelp.gameObject.SetActive(true);sectionHelpClose.rect.gameObject.SetActive(true);
                TopLeft(helpCategory.rectTransform,new Vector2(32,-24),new Vector2(976,24));helpCategory.fontSize=18;
                TopLeft(helpTitle.rectTransform,new Vector2(32,-60),new Vector2(976,40));
                TopLeft(helpDescription.rectTransform,new Vector2(32,-124),new Vector2(976,176));helpDescription.fontSize=24;
                TopLeft(helpDetail.rectTransform,new Vector2(32,-320),new Vector2(976,176));helpDetail.fontSize=20;
                helpCategory.text="HELP";helpCategory.color=accent;
                helpTitle.text=sectionHelpTarget.hintTitle;
                helpDescription.text=SectionHelpDescription(sectionHelpTarget.hintTitle);
                helpDetail.text=sectionHelpTarget.hint();helpDetail.color=ConsoleTheme.Muted;
                return;
            }
            var help=hovered?.help;
            bool visible=AcceptsGameInput&&!Quest.Placing&&!moving&&pressed==null&&help!=null&&Time.unscaledTime-hoverStartedAt>=ActionHelpDelay;
            actionHelp.gameObject.SetActive(visible);
            if(!visible)return;
            sectionHelpBackdrop.gameObject.SetActive(false);sectionHelpClose.rect.gameObject.SetActive(false);
            TopLeft(actionHelp,new Vector2(32,-HudBodyTop),new Vector2(HudInnerWidth,HudSelectionHeight));
            TopLeft(helpCategory.rectTransform,new Vector2(24,-12),new Vector2(HudInnerWidth-48,24));helpCategory.fontSize=16;
            TopLeft(helpTitle.rectTransform,new Vector2(24,-48),new Vector2(280,88));
            TopLeft(helpDescription.rectTransform,new Vector2(340,-44),new Vector2(HudInnerWidth-364,76));helpDescription.fontSize=20;
            TopLeft(helpDetail.rectTransform,new Vector2(340,-124),new Vector2(HudInnerWidth-364,44));helpDetail.fontSize=18;
            var tint=ActionTint(help.Category);
            helpCategory.text=CategoryLabel(help.Category)+" · FIELD GUIDE";helpCategory.color=tint;
            helpTitle.text=help.Label;helpDescription.text=help.Description;
            helpDetail.text=help.HelpDetail;helpDetail.color=tint;
        }
        private static string SectionHelpDescription(string title)
        {
            switch(title)
            {
                case "SELECTION":return "Each card groups the selected units or buildings of one type. Its number shows how many are selected; the bar shows their combined health. Damaged units, unfinished buildings and casters also show health, construction progress or energy.";
                case "OPTIONS":return "These commands belong to your selection. Choose a building to train units or research upgrades, a worker to construct, or mobile units to give orders. Mixed units and buildings leave this area empty. Pale slots are not buttons; point at a command for its cost and requirements.";
                case "PRODUCTION":return "The wide green card is the first active order. Green borders mark occupied slots 2 to 5; yellow on slot 5 means more orders. Click any occupied card to cancel that exact item and recover its cost, even with several buildings selected.";
                case "CONTROL GROUPS":return "Groups remember your selected units and buildings. Trigger recalls a group. Hold Grip before pressing Trigger to replace it with your current selection; on desktop, use Ctrl + click. Release over the same group to confirm. A flash and controller vibration confirm assignment. Assigning with no selection clears the group.";
                case "QUEUED ORDERS":return "The large green card is the current action. Follow the arrows through pending orders, left to right and then the second row. Click any card to cancel that order. Cancel all appears in the header only when there is more than one order.";
                default:return "Read these controls, then close this window to return to your selection. The match continues while this guide is open.";
            }
        }
        private static string CategoryLabel(ActionCategory category)
        {
            switch(category)
            {
                case ActionCategory.Build:return "BUILD STRUCTURES";
                case ActionCategory.Train:return "TRAIN UNITS";
                case ActionCategory.Research:return "RESEARCH";
                case ActionCategory.Abilities:return "CASTER ABILITIES";
                case ActionCategory.Cancel:return "CANCEL / REFUND";
                default:return "UNIT ORDERS";
            }
        }
        private static Color ActionTint(ActionCategory category)
        {
            switch(category)
            {
                case ActionCategory.Build:return new Color(1,.72f,.35f);
                case ActionCategory.Train:return new Color(.36f,.92f,.73f);
                case ActionCategory.Research:return new Color(.8f,.65f,1);
                case ActionCategory.Abilities:return new Color(.86f,.53f,1);
                case ActionCategory.Cancel:return new Color(1,.59f,.53f);
                default:return new Color(.4f,.78f,1);
            }
        }
    }
}
