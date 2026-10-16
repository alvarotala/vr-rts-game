using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const float ActionHelpDelay=1.5f;
        private float hoverStartedAt;
        private RectTransform actionHelp;
        private Text helpCategory,helpTitle,helpDescription,helpDetail;
        private Image helpAccent;

        private void CreateActionHelp()
        {
            // Replace the selection card temporarily, leaving actions, resources and queues visible.
            actionHelp=Section(root,"Action help",new Vector2(24,-178),new Vector2(320,296),new Color(.025f,.065f,.095f,1));
            helpAccent=Section(actionHelp,"Category accent",Vector2.zero,new Vector2(4,296),accent).GetComponent<Image>();
            helpCategory=Label(actionHelp,"",new Vector2(16,-10),new Vector2(288,24),14,accent);
            helpTitle=Label(actionHelp,"",new Vector2(16,-40),new Vector2(288,34),24,Color.white);
            helpDescription=Label(actionHelp,"",new Vector2(16,-84),new Vector2(288,140),18,new Color(.85f,.9f,.93f),TextAnchor.UpperLeft);
            helpDetail=Label(actionHelp,"",new Vector2(16,-230),new Vector2(288,56),16,accent,TextAnchor.UpperLeft);
            actionHelp.gameObject.SetActive(false);
        }
        private void HideActionHelp()
        {
            if(actionHelp!=null)actionHelp.gameObject.SetActive(false);
            hoverStartedAt=Time.unscaledTime;
        }
        private void UpdateActionHelp()
        {
            if(actionHelp==null)return;
            var help=hovered?.help;
            bool visible=AcceptsGameInput&&!Quest.Placing&&!moving&&pressed==null&&help!=null&&Time.unscaledTime-hoverStartedAt>=ActionHelpDelay;
            actionHelp.gameObject.SetActive(visible);
            if(!visible)return;
            var tint=ActionTint(help.Category);
            helpCategory.text=CategoryLabel(help.Category)+" · FIELD GUIDE";helpCategory.color=tint;
            helpAccent.color=tint;helpTitle.text=help.Label;helpDescription.text=help.Description;
            helpDetail.text=help.HelpDetail;helpDetail.color=tint;
        }
        private static string CategoryLabel(ActionCategory category)
        {
            switch(category)
            {
                case ActionCategory.Build:return "BUILD STRUCTURES";
                case ActionCategory.Train:return "TRAIN UNITS";
                case ActionCategory.Research:return "RESEARCH";
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
                case ActionCategory.Cancel:return new Color(1,.59f,.53f);
                default:return new Color(.4f,.78f,1);
            }
        }
    }
}
