using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private RectTransform controlGuide;
        private Text controlGuideDevice;
        private int controlGuideTopic,controlGuidePage,controlGuidePages=1;
        private bool controlGuideTouch;
        private readonly Target[] controlGuideTabs=new Target[6];
        private readonly Text[] controlGuideTabLabels=new Text[6];
        private readonly RectTransform[] controlGuideCards=new RectTransform[8];
        private readonly Text[] controlGuideCommands=new Text[8],controlGuideGestures=new Text[8],controlGuideDescriptions=new Text[8];

        private void CreateControlGuide()
        {
            controlGuide=Section(root,"Control guide cards",new Vector2(30,-216),new Vector2(1040,554),new Color(.035f,.08f,.12f));
            Label(controlGuide,"CONTROL GUIDE",new Vector2(16,-10),new Vector2(450,26),21,accent);
            controlGuideDevice=Label(controlGuide,"",new Vector2(470,-10),new Vector2(554,26),16,Color.white,TextAnchor.MiddleRight);
            for(int i=0;i<controlGuideTabs.Length;i++)
            {
                int index=i;
                var tab=Section(controlGuide,"Help category "+i,new Vector2(16+i*169,-46),new Vector2(163,42));
                controlGuideTabLabels[i]=Label(tab,"",new Vector2(4,0),new Vector2(155,42),17,Color.white,TextAnchor.MiddleCenter);
                controlGuideTabs[i]=new Target{rect=tab,image=tab.GetComponent<Image>(),normal=button,click=()=>{controlGuideTopic=index;controlGuidePage=0;Rebuild();UpdateMenuText();}};
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
            bool visible=Page==MenuPage.Guide;
            controlGuide.gameObject.SetActive(visible);if(!visible)return;
            var topics=DefaultControlCatalog.For(Quest.Active,Bindings);controlGuideTopic=Mathf.Clamp(controlGuideTopic,0,topics.Count-1);
            if(controlGuideTouch!=Quest.Active){controlGuideTouch=Quest.Active;controlGuidePage=0;}
            controlGuideDevice.text=Quest.Active?Bindings.HandName.ToUpperInvariant()+" TOUCH · "+Bindings.OtherHandName+" unused":"MOUSE / KEYBOARD";
            for(int i=0;i<topics.Count;i++)
            {
                var tab=controlGuideTabs[i];tab.rect.name="Help: "+topics[i].Name;
                tab.normal=i==controlGuideTopic?MenuPalette.Primary:button;tab.image.color=tab.normal;
                controlGuideTabLabels[i].text=topics[i].Name;
                targets.Add(tab);
            }
            var commands=topics[controlGuideTopic].Commands;
            controlGuidePages=Mathf.Max(1,(commands.Count+controlGuideCards.Length-1)/controlGuideCards.Length);
            controlGuidePage=Mathf.Clamp(controlGuidePage,0,controlGuidePages-1);
            if(controlGuidePages>1)((LocalizedText)controlGuideDevice).Append($" · PAGE {controlGuidePage+1}/{controlGuidePages}");
            for(int i=0;i<controlGuideCards.Length;i++)
            {
                int index=controlGuidePage*controlGuideCards.Length+i;
                bool active=index<commands.Count;controlGuideCards[i].gameObject.SetActive(active);if(!active)continue;
                controlGuideCommands[i].text=commands[index].Name;controlGuideGestures[i].text=commands[index].Gesture;
                controlGuideDescriptions[i].text=commands[index].Description;
            }
        }

    }
}
