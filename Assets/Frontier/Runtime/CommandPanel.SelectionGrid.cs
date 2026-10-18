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
        private const float SelectionDoubleClick=.35f,SelectionHold=.65f;
        private RectTransform selectionGrid;
        private Text selectionGridTitle,selectionGridHint,selectionPageLabel;
        private readonly Target[] selectionTiles=new Target[SelectionPageSize];
        private readonly SelectionPortrait[] selectionPortraits=new SelectionPortrait[SelectionPageSize];
        private readonly Text[] selectionNames=new Text[SelectionPageSize];
        private readonly Image[] selectionHealthBars=new Image[SelectionPageSize],selectionHoldBars=new Image[SelectionPageSize];
        private Target previousSelectionPage,nextSelectionPage;
        private int selectionPage,pendingSelectionId;
        private float pendingSelectionAt;
        private int[] gestureSelection=Array.Empty<int>();
        private Simulation selectionGestureMatch;
        private bool selectionSecondClick;
        private string selectionRoster="";

        private VisibleEntity[] SelectionMembers()=>Session.View==null?Array.Empty<VisibleEntity>():Session.View.entities
            .Where(e=>Session.Selection.Contains(e.id)&&e.owner==Session.View.player&&!e.remembered).OrderBy(e=>e.definition).ThenBy(e=>e.id).ToArray();

        private void CreateSelectionGrid()
        {
            selectionGrid=Section(matchHud,"Selection grid",new Vector2(24,-484),new Vector2(1052,172));
            selectionGridTitle=Label(selectionGrid,"SELECTED ITEMS",new Vector2(16,-8),new Vector2(650,24),16,accent);
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
                selectionHoldBars[i]=Section(card,"Remove progress",new Vector2(0,0),new Vector2(0,4),new Color(1,.25f,.2f)).GetComponent<Image>();
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
            int pages=(members.Length+SelectionPageSize-1)/SelectionPageSize;
            selectionPage=Mathf.Clamp(selectionPage,0,pages-1);
            int count=Mathf.Min(SelectionPageSize,members.Length-selectionPage*SelectionPageSize),rows=(count+SelectionColumns-1)/SelectionColumns;
            float height=68+rows*104;
            selectionGrid.sizeDelta=new Vector2(1052,height);
            selectionGridHint.rectTransform.anchoredPosition=new Vector2(16,-40-rows*104);
            selectionGridTitle.text=$"SELECTED ITEMS · {members.Length}";
            selectionPageLabel.text=$"{selectionPage+1} / {pages}";
            selectionPageLabel.gameObject.SetActive(pages>1);
            previousSelectionPage.rect.gameObject.SetActive(selectionPage>0);
            nextSelectionPage.rect.gameObject.SetActive(selectionPage+1<pages);
            for(int i=0;i<SelectionPageSize;i++)
            {
                var target=selectionTiles[i];target.rect.gameObject.SetActive(i<count);target.selectionId=0;
                if(i>=count)continue;
                var member=members[selectionPage*SelectionPageSize+i];var type=Session.Catalog[member.definition];
                target.selectionId=member.id;target.rect.name="Selection item "+member.id;target.image.color=target.normal;
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
            foreach(var tile in selectionTiles)
            {
                if(tile.selectionId==0)continue;
                int index=Array.IndexOf(selectionTiles,tile);
                var entity=Session.View.entities.FirstOrDefault(e=>e.id==tile.selectionId);
                float ratio=entity==null?0:Mathf.Clamp01(entity.health/Session.Catalog[entity.definition].health);
                selectionHealthBars[index].rectTransform.sizeDelta=new Vector2(84*ratio,3);
                selectionHealthBars[index].color=ratio<.3f?new Color(1,.3f,.2f):SupplyTint;
            }
            UpdateSelectionHover(Time.unscaledTime);
        }
        private void UpdateSelectionHover(float now)
        {
            string hint=Quest.Active?"Trigger / A: select · Double: same type in selection · Hold: remove":"Click: select · Double-click: same type in selection · Hold: remove";
            for(int i=0;i<SelectionPageSize;i++)
            {
                var tile=selectionTiles[i];if(tile.selectionId==0)continue;
                bool holding=pressed==tile&&hovered==tile;
                float progress=holding?Mathf.Clamp01((now-pressedAt)/SelectionHold):0;
                selectionHoldBars[i].rectTransform.sizeDelta=new Vector2(94*progress,4);
                tile.image.color=holding?Color.Lerp(button,QueueCancelTint,progress):hovered==tile||pendingSelectionId==tile.selectionId?new Color(.12f,.32f,.39f):button;
                if(hovered==tile)
                {
                    var entity=Session.View?.entities.FirstOrDefault(e=>e.id==tile.selectionId);
                    if(entity!=null)hint=(holding?"Hold to remove · ":"")+Session.Catalog[entity.definition].name+" #"+entity.id+(holding?"":" · Double: same type · Hold: remove");
                }
            }
            selectionGridHint.text=hint;
        }
        private bool SelectionGestureValid()=>AcceptsGameInput&&!Quest.Placing&&!moving&&selectionGestureMatch==Session.Match
            &&Session.Selection.SetEquals(gestureSelection);
        private void ClearSelectionGesture()
        {pendingSelectionId=0;selectionSecondClick=false;selectionGestureMatch=null;gestureSelection=Array.Empty<int>();}
        private void TickSelectionClick(float now)
        {
            if(pendingSelectionId==0)return;
            if(!SelectionGestureValid()){ClearSelectionGesture();return;}
            // Wait for the second press/release before committing a single click. This keeps
            // the original selection (including other pages) available to the double click.
            if(pressed?.selectionId>0||now-pendingSelectionAt<SelectionDoubleClick)return;
            int id=pendingSelectionId;ClearSelectionGesture();Session.Select(id,false);
        }
        private bool PointSelectionGrid(Target hit,bool down,bool up,float now)
        {
            TickSelectionClick(now);
            if(!AcceptsGameInput||Quest.Placing){ClearSelectionGesture();return false;}
            if(down)
            {
                if(hit?.selectionId>0)
                {
                    bool twice=pendingSelectionId==hit.selectionId&&SelectionGestureValid()&&now-pendingSelectionAt<=SelectionDoubleClick;
                    ClearSelectionGesture();selectionSecondClick=twice;gestureSelection=Session.Selection.ToArray();selectionGestureMatch=Session.Match;
                    pressed=hit;pressedAt=now;HideActionHelp();UpdateSelectionHover(now);return true;
                }
                ClearSelectionGesture();
            }
            if(pressed?.selectionId>0)
            {
                var target=pressed;
                // Leaving the card cancels the whole gesture, even if the pointer returns.
                if(hit!=target||!SelectionGestureValid())
                {pressed=null;ClearSelectionGesture();UpdateSelectionHover(now);return true;}
                if(now-pressedAt>=SelectionHold)
                {
                    int id=target.selectionId;pressed=null;ClearSelectionGesture();Session.Deselect(id);nextText=0;
                    UpdateSelectionHover(now);return true;
                }
                if(up)
                {
                    pressed=null;
                    if(selectionSecondClick)
                    {int id=target.selectionId;ClearSelectionGesture();Session.SelectSameTypeInSelection(id);nextText=0;}
                    else{pendingSelectionId=target.selectionId;pendingSelectionAt=now;}
                    UpdateSelectionHover(now);return true;
                }
                UpdateSelectionHover(now);return true;
            }
            UpdateSelectionHover(now);return false;
        }
    }
}
