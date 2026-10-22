using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const int HotkeyRowsPerPage=5;
        private RectTransform hotkeys;
        private bool hotkeysTouch;
        private bool hotkeysEditing=true;
        private int hotkeysTopic,hotkeysPage;
        private Text hotkeysCount,hotkeysInputHeader,hotkeysDevice;
        private Target hotkeysPrevious,hotkeysNext,hotkeysEditTab;
        private readonly Target[] hotkeyChangeButtons=new Target[HotkeyRowsPerPage];
        private RectTransform hotkeyModal;
        private Text hotkeyModalTitle,hotkeyModalDetail;
        private Target hotkeyCancel,hotkeyConfirm;
        private string editingHotkey,pendingHotkey,hotkeyMessage="Choose an input to change. Changes are saved on this device.";
        private bool hotkeyCaptureReady,hotkeyResetPending,hotkeyReleasePending;
        private float hotkeyDeadline;
        private bool HotkeyModalOpen=>editingHotkey!=null||hotkeyResetPending;
        private readonly Target[] hotkeysCategories=new Target[6];
        private readonly RectTransform[] hotkeyRows=new RectTransform[HotkeyRowsPerPage];
        private readonly Text[] hotkeyNames=new Text[HotkeyRowsPerPage],hotkeyBindings=new Text[HotkeyRowsPerPage],hotkeyDetails=new Text[HotkeyRowsPerPage];

        private void CreateHotkeys()
        {
            hotkeys=Section(root,"Hotkey reference",new Vector2(30,-216),new Vector2(1040,634),new Color(.035f,.08f,.12f));
            hotkeysDevice=Label(hotkeys,"",new Vector2(16,-8),new Vector2(500,40),19,accent);
            hotkeysEditTab=HotkeyButton("Edit bindings",new Vector2(534,-8),new Vector2(230,40),()=>{hotkeysEditing=true;hotkeysPage=0;Rebuild();});
            Label(hotkeys,"SAVED LOCALLY",new Vector2(778,-8),new Vector2(246,40),16,accent,TextAlignmentOptions.Right);
            var topics=DefaultControlCatalog.For(true);
            for(int i=0;i<hotkeysCategories.Length;i++)
            {
                int index=i;
                hotkeysCategories[i]=HotkeyButton(topics[i].Name,new Vector2(16+i*169,-58),new Vector2(163,40),()=>{hotkeysEditing=false;hotkeysTopic=index;hotkeysPage=0;Rebuild();});
                hotkeysCategories[i].rect.name="Hotkeys: "+topics[i].Name;
            }
            Label(hotkeys,"COMMAND",new Vector2(28,-108),new Vector2(212,24),15,accent);
            hotkeysInputHeader=Label(hotkeys,"INPUT · CLICK TO CHANGE",new Vector2(264,-108),new Vector2(288,24),15,accent);
            Label(hotkeys,"WHEN / WHAT IT DOES",new Vector2(574,-108),new Vector2(438,24),15,accent);
            for(int i=0;i<HotkeyRowsPerPage;i++)
            {
                var row=Section(hotkeys,"Hotkey row "+i,new Vector2(16,-136-i*86),new Vector2(1008,80),new Color(.055f,.12f,.17f));
                hotkeyRows[i]=row;
                hotkeyNames[i]=Label(row,"",new Vector2(12,-8),new Vector2(212,64),18,Color.white,TextAlignmentOptions.Left);
                hotkeyBindings[i]=Label(row,"",new Vector2(248,-8),new Vector2(288,64),17,accent,TextAlignmentOptions.Left);
                hotkeyDetails[i]=Label(row,"",new Vector2(558,-5),new Vector2(438,70),16,new Color(.73f,.83f,.87f),TextAlignmentOptions.Left);
                int slot=i;
                var change=Section(row,"Change binding",new Vector2(240,-4),new Vector2(304,72),button);
                hotkeyBindings[i].transform.SetAsLastSibling();
                hotkeyChangeButtons[i]=new Target{rect=change,image=change.GetComponent<Image>(),normal=button,click=()=>BeginHotkeyChange(HotkeyBindings.Definitions(hotkeysTouch)[hotkeysPage*HotkeyRowsPerPage+slot].Id)};
            }
            hotkeysPrevious=HotkeyButton("Previous",new Vector2(16,-580),new Vector2(155,38),()=>{hotkeysPage--;Rebuild();});
            hotkeysNext=HotkeyButton("Next",new Vector2(869,-580),new Vector2(155,38),()=>{hotkeysPage++;Rebuild();});
            hotkeysCount=Label(hotkeys,"",new Vector2(185,-580),new Vector2(670,38),18,Color.white,TextAlignmentOptions.Center);
            hotkeyModal=Section(hotkeys,"Change hotkey",new Vector2(16,-136),new Vector2(1008,430),new Color(.055f,.12f,.17f));
            hotkeyModalTitle=Label(hotkeyModal,"",new Vector2(28,-24),new Vector2(952,70),27,accent);
            hotkeyModalDetail=Label(hotkeyModal,"",new Vector2(28,-108),new Vector2(952,190),22,Color.white,TextAlignmentOptions.TopLeft);
            hotkeyCancel=ModalButton("Cancel change",new Vector2(28,-344),CancelHotkeyChange);
            hotkeyConfirm=ModalButton("Confirm change",new Vector2(518,-344),ConfirmHotkeyChange);
        }

        private Target ModalButton(string label,Vector2 position,System.Action click)
        {
            var rect=Section(hotkeyModal,label,position,new Vector2(462,58),button);
            Label(rect,label,new Vector2(8,0),new Vector2(446,58),22,Color.white,TextAlignmentOptions.Center);
            return new Target{rect=rect,image=rect.GetComponent<Image>(),normal=button,click=click};
        }

        private Target HotkeyButton(string label,Vector2 position,Vector2 size,System.Action click)
        {
            var rect=Section(hotkeys,label,position,size,button);
            Label(rect,label,new Vector2(4,0),new Vector2(size.x-8,size.y),17,Color.white,TextAlignmentOptions.Center);
            return new Target{rect=rect,image=rect.GetComponent<Image>(),normal=button,click=click};
        }

        private void BuildHotkeys()
        {
            hotkeys.gameObject.SetActive(Page==MenuPage.Hotkeys);if(Page!=MenuPage.Hotkeys)return;
            if(hotkeysTouch!=Quest.Active){DismissHotkeyModal();hotkeysTouch=Quest.Active;hotkeysPage=0;}
            hotkeysDevice.text=hotkeysTouch?"ACTIVE · "+Bindings.HandName+" Touch controller":"ACTIVE · Mouse / Keyboard";
            hotkeyModal.gameObject.SetActive(HotkeyModalOpen);
            var topics=DefaultControlCatalog.For(hotkeysTouch,Bindings);
            hotkeysTopic=Mathf.Clamp(hotkeysTopic,0,topics.Count-1);
            var commands=topics[hotkeysTopic].Commands;
            var definitions=HotkeyBindings.Definitions(hotkeysTouch);
            int count=hotkeysEditing?definitions.Count:commands.Count;
            int pages=(count+HotkeyRowsPerPage-1)/HotkeyRowsPerPage;
            hotkeysPage=Mathf.Clamp(hotkeysPage,0,pages-1);
            if(HotkeyModalOpen)
            {
                foreach(var row in hotkeyRows)row.gameObject.SetActive(false);
                hotkeysPrevious.rect.gameObject.SetActive(false);hotkeysNext.rect.gameObject.SetActive(false);hotkeysCount.text="";
                hotkeyConfirm.rect.gameObject.SetActive(pendingHotkey!=null||hotkeyResetPending);
                targets.Add(hotkeyCancel);if(hotkeyConfirm.rect.gameObject.activeSelf)targets.Add(hotkeyConfirm);
                return;
            }
            AddHotkeyTab(hotkeysEditTab,hotkeysEditing);
            for(int i=0;i<hotkeysCategories.Length;i++)AddHotkeyTab(hotkeysCategories[i],!hotkeysEditing&&i==hotkeysTopic);
            hotkeysInputHeader.text=hotkeysEditing?"INPUT · CLICK TO CHANGE":"CURRENT INPUT / MENU ACTION";
            int first=hotkeysPage*HotkeyRowsPerPage;
            for(int i=0;i<HotkeyRowsPerPage;i++)
            {
                int index=first+i;bool visible=index<count;
                hotkeyRows[i].gameObject.SetActive(visible);if(!visible)continue;
                hotkeyChangeButtons[i].rect.gameObject.SetActive(hotkeysEditing);
                if(hotkeysEditing)
                {
                    var definition=definitions[index];hotkeyRows[i].name="Binding: "+definition.Id;
                    hotkeyChangeButtons[i].rect.name="Change: "+definition.Id;
                    hotkeyNames[i].text=definition.Name;hotkeyBindings[i].text=Bindings.Name(hotkeysTouch,definition.Id)+"   ·   Change";hotkeyDetails[i].text=definition.Description;
                    targets.Add(hotkeyChangeButtons[i]);
                }
                else
                {
                    var command=commands[index];hotkeyRows[i].name="Hotkey: "+command.Id;
                    hotkeyNames[i].text=command.Name;hotkeyBindings[i].text=command.Gesture;hotkeyDetails[i].text=command.Description;
                }
            }
            hotkeysCount.text=$"{(hotkeysEditing?"Gameplay bindings":topics[hotkeysTopic].Name)} · {first+1}–{Mathf.Min(first+HotkeyRowsPerPage,count)} of {count} · Page {hotkeysPage+1}/{pages}";
            hotkeysPrevious.rect.gameObject.SetActive(hotkeysPage>0);if(hotkeysPage>0)targets.Add(hotkeysPrevious);
            hotkeysNext.rect.gameObject.SetActive(hotkeysPage+1<pages);if(hotkeysPage+1<pages)targets.Add(hotkeysNext);
        }

        private void AddHotkeyTab(Target tab,bool selected)
        {tab.normal=selected?MenuPalette.Primary:button;tab.image.color=tab.normal;targets.Add(tab);}

        private void BeginHotkeyChange(string id)
        {
            editingHotkey=id;pendingHotkey=null;hotkeyCaptureReady=false;hotkeyDeadline=Time.unscaledTime+12;
            var definition=System.Linq.Enumerable.First(HotkeyBindings.Definitions(hotkeysTouch),d=>d.Id==id);
            hotkeyModalTitle.text="CHANGE · "+definition.Name;
            hotkeyModalDetail.text="Current input: "+Bindings.Name(hotkeysTouch,id)+"\n\n"+
                (hotkeysTouch?"Release all buttons, then press a button on the "+Bindings.HandName.ToLowerInvariant()+" Touch controller.":definition.Modifier?"Release all keys, then press Shift, Ctrl or Alt.":"Release all inputs, then press a key or mouse button. Escape cancels.");
            hotkeyMessage="Waiting for a new input…";Rebuild();
        }
        private void RequestHotkeyReset()
        {
            hotkeyResetPending=true;hotkeyModalTitle.text="RESTORE DEFAULTS?";
            hotkeyModalDetail.text="Restore all "+(hotkeysTouch?"Quest Touch":"mouse / keyboard")+" gameplay bindings?\n\nBindings for the other device will be kept.";
            hotkeyConfirm.rect.GetComponentInChildren<Text>().text="Restore defaults";Rebuild();
        }
        private void OfferHotkey(string candidate)
        {
            if(!Bindings.Supports(hotkeysTouch,editingHotkey,candidate))
            {
                Bindings.Assign(hotkeysTouch,editingHotkey,candidate,false,out string invalidError);
                hotkeyMessage=invalidError;hotkeyCaptureReady=false;hotkeyDeadline=Time.unscaledTime+12;return;
            }
            string conflict=Bindings.Conflict(hotkeysTouch,editingHotkey,candidate);
            if(conflict!=null)
            {
                pendingHotkey=candidate;
                string other=System.Linq.Enumerable.First(HotkeyBindings.Definitions(hotkeysTouch),d=>d.Id==conflict).Name;
                hotkeyModalDetail.text=HotkeyBindings.Display(candidate,Bindings.LeftHand)+" is used by "+other+".\n\nSwap the two inputs?\n"+other+" will use "+Bindings.Name(hotkeysTouch,editingHotkey)+".";
                hotkeyConfirm.rect.GetComponentInChildren<Text>().text="Swap bindings";hotkeyMessage="Confirm the swap or cancel to keep both bindings.";Rebuild();return;
            }
            if(Bindings.Assign(hotkeysTouch,editingHotkey,candidate,false,out string error))SaveHotkeyChange();
            else{hotkeyMessage=error;hotkeyCaptureReady=false;hotkeyDeadline=Time.unscaledTime+12;}
        }
        private void ConfirmHotkeyChange()
        {
            if(hotkeyResetPending){Bindings.Reset(hotkeysTouch);SaveHotkeyChange();return;}
            if(pendingHotkey==null)return;
            if(Bindings.Assign(hotkeysTouch,editingHotkey,pendingHotkey,true,out string error))SaveHotkeyChange();
            else hotkeyMessage=error;
        }
        private void SaveHotkeyChange()
        {Bindings.Save();DismissHotkeyModal();hotkeyMessage="Saved locally. Gameplay and the control guide now use these bindings.";Rebuild();}
        private void DismissHotkeyModal()
        {
            if(HotkeyModalOpen)hotkeyReleasePending=true;
            editingHotkey=pendingHotkey=null;hotkeyResetPending=hotkeyCaptureReady=false;
        }
        private void CancelHotkeyChange(){DismissHotkeyModal();hotkeyMessage="Change cancelled. Your bindings were kept.";Rebuild();}

        // Called before gameplay routing. A captured press and its release are never
        // allowed to select, issue an order, place the board or resume the match.
        public bool CaptureHotkeyInput(Ray ray)
        {
            if(hotkeyReleasePending)
            {
                if(!HotkeyInput.AnyHeld(false)&&(!Quest.Active||!HotkeyInput.AnyHeld(true,Quest.ActiveController)))hotkeyReleasePending=false;
                return true;
            }
            if(Page!=MenuPage.Hotkeys||editingHotkey==null||pendingHotkey!=null)return false;
            if(HotkeyInput.Read("key:Escape").Down){CancelHotkeyChange();return true;}
            float remaining=hotkeyDeadline-Time.unscaledTime;
            if(remaining<=0){CancelHotkeyChange();return true;}
            if(hotkeyMessage.StartsWith("Waiting"))hotkeyMessage="Waiting for a new input… "+Mathf.CeilToInt(remaining)+" s · Cancel to keep the current binding.";
            bool held=HotkeyInput.AnyHeld(false)||(Quest.Active&&HotkeyInput.AnyHeld(true,Quest.ActiveController));
            if(!hotkeyCaptureReady){hotkeyCaptureReady=!held;return true;}
            // The fixed pointer can always cancel, even if it is also a candidate input.
            if(Hit(hotkeyCancel.rect,ray,out _))
            {
                bool click=Quest.Active?(HotkeyInput.Read("touch:trigger",Quest.ActiveController).Down||HotkeyInput.Read("touch:a",Quest.ActiveController).Down):HotkeyInput.Read("mouse:left").Down;
                if(click)CancelHotkeyChange();
                return true;
            }
            string candidate=HotkeyInput.Pressed(hotkeysTouch,Quest.ActiveController);
            if(candidate!=null)OfferHotkey(candidate);
            return true;
        }
    }
}
