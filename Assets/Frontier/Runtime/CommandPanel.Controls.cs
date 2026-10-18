using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private RectTransform controlGuide;
        private Text controlGuideDevice;
        private int controlGuideTopic;
        private readonly Target[] controlGuideTabs=new Target[6];
        private readonly Text[] controlGuideTabLabels=new Text[6];
        private readonly RectTransform[] controlGuideCards=new RectTransform[8];
        private readonly Text[] controlGuideCommands=new Text[8],controlGuideGestures=new Text[8],controlGuideDescriptions=new Text[8];

        private void CreateControlGuide()
        {
            controlGuide=Section(root,"Control guide",new Vector2(30,-390),new Vector2(1040,554),new Color(.035f,.08f,.12f));
            Label(controlGuide,"CONTROL GUIDE",new Vector2(16,-10),new Vector2(450,26),21,accent);
            controlGuideDevice=Label(controlGuide,"",new Vector2(470,-10),new Vector2(554,26),16,Color.white,TextAnchor.MiddleRight);
            for(int i=0;i<controlGuideTabs.Length;i++)
            {
                int index=i;
                var tab=Section(controlGuide,"Help category "+i,new Vector2(16+i*169,-46),new Vector2(163,42));
                controlGuideTabLabels[i]=Label(tab,"",new Vector2(4,0),new Vector2(155,42),17,Color.white,TextAnchor.MiddleCenter);
                controlGuideTabs[i]=new Target{rect=tab,image=tab.GetComponent<Image>(),normal=button,click=()=>{controlGuideTopic=index;Rebuild();UpdateMenuText();}};
            }
            for(int i=0;i<controlGuideCards.Length;i++)
            {
                var card=Section(controlGuide,"Help command "+i,new Vector2(16+(i%2)*510,-104-(i/2)*108),new Vector2(498,100),new Color(.055f,.12f,.17f));
                controlGuideCards[i]=card;
                controlGuideCommands[i]=Label(card,"",new Vector2(12,-7),new Vector2(474,23),19,Color.white);
                controlGuideGestures[i]=Label(card,"",new Vector2(12,-32),new Vector2(474,22),16,accent);
                controlGuideDescriptions[i]=Label(card,"",new Vector2(12,-57),new Vector2(474,38),16,new Color(.73f,.83f,.87f),TextAnchor.UpperLeft);
            }
        }

        private void BuildControlGuide()
        {
            bool visible=Page==MenuPage.Pause;
            controlGuide.gameObject.SetActive(visible);if(!visible)return;
            var topics=DefaultControlCatalog.For(Quest.Active);controlGuideTopic=Mathf.Clamp(controlGuideTopic,0,topics.Count-1);
            controlGuideDevice.text=Quest.Active?"RIGHT TOUCH CONTROLLER · Left unassigned":"MOUSE / KEYBOARD";
            for(int i=0;i<topics.Count;i++)
            {
                var tab=controlGuideTabs[i];tab.rect.name="Help: "+topics[i].Name;
                tab.normal=i==controlGuideTopic?new Color(.12f,.36f,.39f):button;tab.image.color=tab.normal;
                controlGuideTabLabels[i].text=topics[i].Name;
                targets.Add(tab);
            }
            var commands=topics[controlGuideTopic].Commands;
            for(int i=0;i<controlGuideCards.Length;i++)
            {
                bool active=i<commands.Count;controlGuideCards[i].gameObject.SetActive(active);if(!active)continue;
                controlGuideCommands[i].text=commands[i].Name;controlGuideGestures[i].text=commands[i].Gesture;
                controlGuideDescriptions[i].text=commands[i].Description;
            }
        }

    }
}
