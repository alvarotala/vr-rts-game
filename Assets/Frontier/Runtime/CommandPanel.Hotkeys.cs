using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const int HotkeyRowsPerPage=5;
        private RectTransform hotkeys;
        private bool hotkeysTouch;
        private int hotkeysTopic,hotkeysPage;
        private Text hotkeysCount;
        private Target hotkeysTouchTab,hotkeysDesktopTab,hotkeysPrevious,hotkeysNext;
        private readonly Target[] hotkeysCategories=new Target[6];
        private readonly RectTransform[] hotkeyRows=new RectTransform[HotkeyRowsPerPage];
        private readonly Text[] hotkeyNames=new Text[HotkeyRowsPerPage],hotkeyBindings=new Text[HotkeyRowsPerPage],hotkeyDetails=new Text[HotkeyRowsPerPage];

        private void CreateHotkeys()
        {
            hotkeys=Section(root,"Hotkey reference",new Vector2(30,-216),new Vector2(1040,634),new Color(.035f,.08f,.12f));
            hotkeysTouchTab=HotkeyButton("Quest Touch",new Vector2(16,-8),new Vector2(245,40),()=>ChooseHotkeyDevice(true));
            hotkeysDesktopTab=HotkeyButton("Mouse / Keyboard",new Vector2(271,-8),new Vector2(245,40),()=>ChooseHotkeyDevice(false));
            Label(hotkeys,"DEFAULT CONTROLS · READ ONLY",new Vector2(534,-8),new Vector2(490,40),16,accent,TextAnchor.MiddleRight);
            var topics=DefaultControlCatalog.For(true);
            for(int i=0;i<hotkeysCategories.Length;i++)
            {
                int index=i;
                hotkeysCategories[i]=HotkeyButton(topics[i].Name,new Vector2(16+i*169,-58),new Vector2(163,40),()=>{hotkeysTopic=index;hotkeysPage=0;Rebuild();});
                hotkeysCategories[i].rect.name="Hotkeys: "+topics[i].Name;
            }
            Label(hotkeys,"COMMAND",new Vector2(28,-108),new Vector2(212,24),15,accent);
            Label(hotkeys,"DEFAULT INPUT / MENU ACTION",new Vector2(264,-108),new Vector2(288,24),15,accent);
            Label(hotkeys,"WHEN / WHAT IT DOES",new Vector2(574,-108),new Vector2(438,24),15,accent);
            for(int i=0;i<HotkeyRowsPerPage;i++)
            {
                var row=Section(hotkeys,"Hotkey row "+i,new Vector2(16,-136-i*86),new Vector2(1008,80),new Color(.055f,.12f,.17f));
                hotkeyRows[i]=row;
                hotkeyNames[i]=Label(row,"",new Vector2(12,-8),new Vector2(212,64),18,Color.white,TextAnchor.MiddleLeft);
                hotkeyBindings[i]=Label(row,"",new Vector2(248,-8),new Vector2(288,64),17,accent,TextAnchor.MiddleLeft);
                hotkeyDetails[i]=Label(row,"",new Vector2(558,-5),new Vector2(438,70),16,new Color(.73f,.83f,.87f),TextAnchor.MiddleLeft);
            }
            hotkeysPrevious=HotkeyButton("Previous",new Vector2(16,-580),new Vector2(155,38),()=>{hotkeysPage--;Rebuild();});
            hotkeysNext=HotkeyButton("Next",new Vector2(869,-580),new Vector2(155,38),()=>{hotkeysPage++;Rebuild();});
            hotkeysCount=Label(hotkeys,"",new Vector2(185,-580),new Vector2(670,38),18,Color.white,TextAnchor.MiddleCenter);
        }

        private Target HotkeyButton(string label,Vector2 position,Vector2 size,System.Action click)
        {
            var rect=Section(hotkeys,label,position,size,button);
            Label(rect,label,new Vector2(4,0),new Vector2(size.x-8,size.y),17,Color.white,TextAnchor.MiddleCenter);
            return new Target{rect=rect,image=rect.GetComponent<Image>(),normal=button,click=click};
        }

        private void ChooseHotkeyDevice(bool touch)
        {hotkeysTouch=touch;hotkeysPage=0;Rebuild();UpdateMenuText();}

        private void BuildHotkeys()
        {
            hotkeys.gameObject.SetActive(Page==MenuPage.Hotkeys);if(Page!=MenuPage.Hotkeys)return;
            var topics=DefaultControlCatalog.For(hotkeysTouch);
            hotkeysTopic=Mathf.Clamp(hotkeysTopic,0,topics.Count-1);
            var commands=topics[hotkeysTopic].Commands;
            int pages=(commands.Count+HotkeyRowsPerPage-1)/HotkeyRowsPerPage;
            hotkeysPage=Mathf.Clamp(hotkeysPage,0,pages-1);
            AddHotkeyTab(hotkeysTouchTab,hotkeysTouch);AddHotkeyTab(hotkeysDesktopTab,!hotkeysTouch);
            for(int i=0;i<hotkeysCategories.Length;i++)AddHotkeyTab(hotkeysCategories[i],i==hotkeysTopic);
            int first=hotkeysPage*HotkeyRowsPerPage;
            for(int i=0;i<HotkeyRowsPerPage;i++)
            {
                int index=first+i;bool visible=index<commands.Count;
                hotkeyRows[i].gameObject.SetActive(visible);if(!visible)continue;
                var command=commands[index];hotkeyRows[i].name="Hotkey: "+command.Id;
                hotkeyNames[i].text=command.Name;hotkeyBindings[i].text=command.Gesture;hotkeyDetails[i].text=command.Description;
            }
            hotkeysCount.text=$"{topics[hotkeysTopic].Name} · {first+1}–{Mathf.Min(first+HotkeyRowsPerPage,commands.Count)} of {commands.Count} · Page {hotkeysPage+1}/{pages}";
            hotkeysPrevious.rect.gameObject.SetActive(hotkeysPage>0);if(hotkeysPage>0)targets.Add(hotkeysPrevious);
            hotkeysNext.rect.gameObject.SetActive(hotkeysPage+1<pages);if(hotkeysPage+1<pages)targets.Add(hotkeysNext);
        }

        private void AddHotkeyTab(Target tab,bool selected)
        {tab.normal=selected?new Color(.12f,.36f,.39f):button;tab.image.color=tab.normal;targets.Add(tab);}
    }
}
