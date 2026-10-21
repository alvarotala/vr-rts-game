using System;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const int SelectionPageSize=20,SelectionColumns=10;
        private RectTransform selectionGrid;
        private Text selectionGridTitle,selectionGridHint,selectionPageLabel;
        private readonly Target[] selectionTiles=new Target[SelectionPageSize];
        private readonly SelectionPortrait[] selectionPortraits=new SelectionPortrait[SelectionPageSize];
        private readonly Text[] selectionNames=new Text[SelectionPageSize],selectionCounts=new Text[SelectionPageSize];
        private readonly Image[] selectionHealthBars=new Image[SelectionPageSize];
        private Target previousSelectionPage,nextSelectionPage;
        private int selectionPage;
        private int[] gestureSelection=Array.Empty<int>();
        private Simulation selectionGestureMatch;
        private bool selectionRemove;
        private string selectionRoster="";

        private VisibleEntity[] SelectionMembers()=>Session.View==null?Array.Empty<VisibleEntity>():Session.View.entities
            .Where(e=>Session.Selection.Contains(e.id)&&e.owner==Session.View.player&&!e.remembered).OrderBy(e=>e.definition).ThenBy(e=>e.id).ToArray();

        private void CreateSelectionGrid()
        {
            selectionGrid=Section(matchHud,"Selection grid",new Vector2(24,-484),new Vector2(1052,172));
            selectionGridTitle=Label(selectionGrid,"SELECTED TYPES",new Vector2(16,-8),new Vector2(650,24),16,accent);
            previousSelectionPage=HudButton(selectionGrid,"Previous selection page","",new Vector2(772,-6),new Vector2(80,28),()=>ChangeSelectionPage(-1),null,out _);
            nextSelectionPage=HudButton(selectionGrid,"Next selection page","",new Vector2(956,-6),new Vector2(80,28),()=>ChangeSelectionPage(1),null,out _);
            previousSelectionPage.rect.GetComponentInChildren<Text>().text="<";nextSelectionPage.rect.GetComponentInChildren<Text>().text=">";
            selectionPageLabel=Label(selectionGrid,"",new Vector2(856,-6),new Vector2(96,28),15,Color.white,TextAnchor.MiddleCenter);
            for(int i=0;i<SelectionPageSize;i++)
            {
                var card=Section(selectionGrid,"Selection slot",new Vector2(16+(i%SelectionColumns)*102,-40-(i/SelectionColumns)*104),new Vector2(94,94),button);
                var portraitObject=new GameObject("Portrait",typeof(RectTransform),typeof(SelectionPortrait));
                var portrait=portraitObject.GetComponent<SelectionPortrait>();portrait.transform.SetParent(card,false);
                TopLeft(portrait.rectTransform,new Vector2(6,-4),new Vector2(82,54));portrait.raycastTarget=false;selectionPortraits[i]=portrait;
                selectionNames[i]=Label(card,"",new Vector2(3,-58),new Vector2(88,28),12,Color.white,TextAnchor.MiddleCenter);
                Section(card,"Health track",new Vector2(5,-87),new Vector2(84,3),new Color(.12f,.22f,.25f));
                selectionHealthBars[i]=Section(card,"Health",new Vector2(5,-87),new Vector2(84,3),SupplyTint).GetComponent<Image>();
                var count=Section(card,"Count badge",new Vector2(52,-4),new Vector2(38,24),new Color(.025f,.08f,.12f,.95f));
                selectionCounts[i]=Label(count,"",Vector2.zero,new Vector2(38,24),16,Color.white,TextAnchor.MiddleCenter);
                selectionCounts[i].fontStyle=FontStyle.Bold;
                selectionTiles[i]=new Target{rect=card,image=card.GetComponent<Image>(),normal=button};
            }
            selectionGridHint=Label(selectionGrid,"",new Vector2(16,-140),new Vector2(1020,26),15,new Color(.72f,.82f,.87f));
        }
        private float LayoutSelectionGrid(bool hud)
        {
            var members=SelectionMembers();
            string roster=string.Join(",",members.Select(e=>e.id));
            if(roster!=selectionRoster){selectionRoster=roster;selectionPage=0;ClearSelectionGesture();}
            bool show=hud&&members.Length>0;selectionGrid.gameObject.SetActive(show);if(!show)return 0;
            var groups=members.GroupBy(e=>e.definition).ToArray();
            int pages=(groups.Length+SelectionPageSize-1)/SelectionPageSize;
            selectionPage=Mathf.Clamp(selectionPage,0,pages-1);
            int count=Mathf.Min(SelectionPageSize,groups.Length-selectionPage*SelectionPageSize),rows=(count+SelectionColumns-1)/SelectionColumns;
            float height=68+rows*104;
            selectionGrid.sizeDelta=new Vector2(1052,height);
            selectionGridHint.rectTransform.anchoredPosition=new Vector2(16,-40-rows*104);
            selectionGridTitle.text=$"SELECTED · {members.Length} ITEMS · {groups.Length} TYPES";
            selectionPageLabel.text=$"{selectionPage+1} / {pages}";
            selectionPageLabel.gameObject.SetActive(pages>1);
            previousSelectionPage.rect.gameObject.SetActive(selectionPage>0);
            nextSelectionPage.rect.gameObject.SetActive(selectionPage+1<pages);
            for(int i=0;i<SelectionPageSize;i++)
            {
                var target=selectionTiles[i];target.rect.gameObject.SetActive(i<count);target.selectionType=null;
                if(i>=count)continue;
                var group=groups[selectionPage*SelectionPageSize+i];var type=Session.Catalog[group.Key];
                target.selectionType=group.Key;target.rect.name="Selection type "+group.Key;target.image.color=target.normal;
                selectionNames[i].text=type.name;selectionPortraits[i].SetType(type);
            }
            UpdateSelectionGrid();return height+12;
        }
        private void BindSelectionTargets()
        {
            if(!selectionGrid.gameObject.activeInHierarchy)return;
            targets.AddRange(selectionTiles.Where(t=>t.rect.gameObject.activeSelf));
            if(previousSelectionPage.rect.gameObject.activeSelf)targets.Add(previousSelectionPage);
            if(nextSelectionPage.rect.gameObject.activeSelf)targets.Add(nextSelectionPage);
        }
        private void ChangeSelectionPage(int direction)
        {ClearSelectionGesture();selectionPage+=direction;Rebuild();}
        private void UpdateSelectionGrid()
        {
            if(!selectionGrid.gameObject.activeInHierarchy)return;
            var members=SelectionMembers();
            for(int i=0;i<SelectionPageSize;i++)
            {
                var tile=selectionTiles[i];if(tile.selectionType==null)continue;
                var group=members.Where(e=>e.definition==tile.selectionType).ToArray();
                float maximum=Session.Catalog[tile.selectionType].health*group.Length;
                float ratio=maximum>0?Mathf.Clamp01(group.Sum(e=>e.health)/maximum):0;
                selectionCounts[i].text="×"+group.Length;
                selectionHealthBars[i].rectTransform.sizeDelta=new Vector2(84*ratio,3);
                selectionHealthBars[i].color=ratio<.3f?new Color(1,.3f,.2f):SupplyTint;
            }
            UpdateSelectionHover();
        }
        // Panel gestures use fixed navigation inputs, independent of map hotkeys.
        private bool RemoveSelectionTypeHeld=>HotkeyInput.Read(Quest.Active?"touch:grip":"key:LeftShift",Quest.ActiveController).Held;
        private void UpdateSelectionHover()
        {
            string hint=Quest.Active?"Trigger / "+Bindings.MenuConfirmName+": select type · Grip + Trigger: remove type":"Click: select type · Shift + click: remove type";
            for(int i=0;i<SelectionPageSize;i++)
            {
                var tile=selectionTiles[i];if(tile.selectionType==null)continue;
                bool active=hovered==tile;
                bool removing=pressed==tile?selectionRemove:RemoveSelectionTypeHeld;
                tile.image.color=active?(removing?QueueCancelTint:new Color(.12f,.32f,.39f)):button;
                if(active)hint=Session.Catalog[tile.selectionType].name+" "+selectionCounts[i].text+" · "+(removing?"Remove this type from selection":hint);
            }
            selectionGridHint.text=hint;
        }
        private bool SelectionGestureValid()=>AcceptsGameInput&&!Quest.Placing&&!moving&&selectionGestureMatch==Session.Match
            &&Session.Selection.SetEquals(gestureSelection);
        private void ClearSelectionGesture()
        {
            if(pressed?.selectionType!=null)pressed=null;
            selectionRemove=false;selectionGestureMatch=null;gestureSelection=Array.Empty<int>();
        }
        private bool PointSelectionGrid(Target hit,bool down,bool up)
        {
            if(!AcceptsGameInput||Quest.Placing){ClearSelectionGesture();return false;}
            if(down)
            {
                ClearSelectionGesture();
                if(hit?.selectionType!=null)
                {
                    selectionRemove=RemoveSelectionTypeHeld;gestureSelection=Session.Selection.ToArray();selectionGestureMatch=Session.Match;
                    pressed=hit;pressedAt=Time.unscaledTime;HideActionHelp();
                }
            }
            if(pressed?.selectionType!=null)
            {
                var target=pressed;
                // Leaving the card cancels the gesture, even if the pointer returns.
                if(hit!=target||!SelectionGestureValid())
                {ClearSelectionGesture();UpdateSelectionHover();return true;}
                if(up)
                {
                    string type=target.selectionType;bool remove=selectionRemove;ClearSelectionGesture();
                    Session.SelectTypeInSelection(type,remove);nextText=0;
                }
                UpdateSelectionHover();return true;
            }
            UpdateSelectionHover();return false;
        }
    }
}
