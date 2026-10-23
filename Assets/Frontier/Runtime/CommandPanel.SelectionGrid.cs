using System;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const int SelectionPageSize=14,SelectionColumns=6,CompactSelectionColumns=7;
        private RectTransform selectionGrid;
        private Text selectionGridTitle,selectionGridHint,selectionPageLabel;
        private readonly Target[] selectionTiles=new Target[SelectionPageSize];
        private readonly SelectionPortrait[] selectionPortraits=new SelectionPortrait[SelectionPageSize];
        private readonly Text[] selectionNames=new Text[SelectionPageSize],selectionCounts=new Text[SelectionPageSize];
        private readonly Text[] selectionEnergy=new Text[SelectionPageSize];
        private readonly Image[] selectionHealthBars=new Image[SelectionPageSize];
        private readonly RectTransform[] selectionHealthTracks=new RectTransform[SelectionPageSize];
        private Target previousSelectionPage,nextSelectionPage;
        private int selectionPage,selectionLayoutColumns=SelectionColumns;
        private int[] gestureSelection=Array.Empty<int>();
        private object selectionGestureMatch;
        private bool selectionRemove;
        private string selectionRoster="";

        private VisibleEntity[] SelectionMembers()=>Session.View==null?Array.Empty<VisibleEntity>():Session.View.entities
            .Where(e=>Session.Selection.Contains(e.id)&&e.owner==Session.View.player&&!e.remembered).OrderBy(e=>e.definition).ThenBy(e=>e.id).ToArray();

        private void CreateSelectionGrid()
        {
            float width=HudInnerWidth-HudPadding*2,tileWidth=(width-(SelectionColumns-1)*HudGap)/SelectionColumns;
            selectionGrid=Section(selectedCard,"Selection grid",new Vector2(HudPadding,-HudContentTop),new Vector2(width,136));
            selectionGrid.GetComponent<Image>().enabled=false;
            selectionGridTitle=Label(selectionGrid,"SELECTED TYPES",new Vector2(16,-8),new Vector2(650,24),16,ConsoleTheme.Muted);
            selectionGridTitle.fontStyle=FontStyle.Bold;
            selectionGridTitle.gameObject.SetActive(false);
            previousSelectionPage=HudButton(selectionGrid,"Previous selection page","",new Vector2(772,-6),new Vector2(80,28),()=>ChangeSelectionPage(-1),null,out _);
            nextSelectionPage=HudButton(selectionGrid,"Next selection page","",new Vector2(956,-6),new Vector2(80,28),()=>ChangeSelectionPage(1),null,out _);
            previousSelectionPage.rect.GetComponentInChildren<Text>().text="<";nextSelectionPage.rect.GetComponentInChildren<Text>().text=">";
            selectionPageLabel=Label(selectionGrid,"",new Vector2(856,-6),new Vector2(96,28),15,ConsoleTheme.Ink,TextAnchor.MiddleCenter);
            for(int i=0;i<SelectionPageSize;i++)
            {
                var card=Section(selectionGrid,"Selection slot",new Vector2((i%SelectionColumns)*(tileWidth+HudGap),-(i/SelectionColumns)*(136+HudGap)),new Vector2(tileWidth,136),button);
                var portraitObject=new GameObject("Portrait",typeof(RectTransform),typeof(SelectionPortrait));
                var portrait=portraitObject.GetComponent<SelectionPortrait>();portrait.transform.SetParent(card,false);
                TopLeft(portrait.rectTransform,new Vector2(16,-44),new Vector2(48,48));portrait.raycastTarget=false;selectionPortraits[i]=portrait;
                selectionNames[i]=Label(card,"",new Vector2(76,-38),new Vector2(tileWidth-92,60),20,ConsoleTheme.Ink,TextAnchor.MiddleLeft);
                selectionNames[i].fontStyle=FontStyle.Bold;
                selectionHealthTracks[i]=Section(card,"Health track",new Vector2(16,-116),new Vector2(tileWidth-32,4),ConsoleTheme.Track);
                selectionHealthBars[i]=Section(card,"Health",new Vector2(16,-116),new Vector2(tileWidth-32,4),SupplyTint).GetComponent<Image>();
                var count=Section(card,"Count badge",new Vector2(tileWidth-70,-10),new Vector2(54,26),Color.clear);
                selectionCounts[i]=Label(count,"",Vector2.zero,new Vector2(54,26),20,ConsoleTheme.Ink,TextAnchor.MiddleRight);
                selectionCounts[i].fontStyle=FontStyle.Bold;
                selectionEnergy[i]=Label(card,"",new Vector2(16,-10),new Vector2(Mathf.Min(130,tileWidth-102),26),18,new Color(.8f,.65f,1));
                selectionEnergy[i].resizeTextForBestFit=true;selectionEnergy[i].resizeTextMinSize=16;selectionEnergy[i].resizeTextMaxSize=18;
                selectionEnergy[i].gameObject.SetActive(false);
                selectionTiles[i]=new Target{rect=card,image=card.GetComponent<Image>(),normal=button};
            }
            selectionGridHint=Label(selectionGrid,"",new Vector2(16,-140),new Vector2(1020,26),15,ConsoleTheme.Muted);
            selectionGridHint.gameObject.SetActive(false);
        }
        private void LayoutSelectionGrid(bool hud)
        {
            var members=SelectionMembers();
            string roster=string.Join(",",members.Select(e=>e.id));
            if(roster!=selectionRoster){selectionRoster=roster;selectionPage=0;ClearSelectionGesture();}
            bool show=hud&&members.Length>0;selectionGrid.gameObject.SetActive(show);if(!show)return;
            var groups=members.GroupBy(e=>e.definition).ToArray();
            int pages=(groups.Length+SelectionPageSize-1)/SelectionPageSize;
            selectionPage=Mathf.Clamp(selectionPage,0,pages-1);
            int count=Mathf.Min(SelectionPageSize,groups.Length-selectionPage*SelectionPageSize);
            bool compact=groups.Length>SelectionColumns;
            selectionLayoutColumns=compact?CompactSelectionColumns:SelectionColumns;
            float tileHeight=compact?60:136,width=HudInnerWidth-HudPadding*2;
            float tileWidth=(width-(selectionLayoutColumns-1)*HudGap)/selectionLayoutColumns,rowStride=tileHeight+HudGap;
            selectionGrid.SetParent(selectedCard,false);
            TopLeft(selectionGrid,new Vector2(HudPadding,-HudContentTop),new Vector2(width,136));
            TopLeft(previousSelectionPage.rect,new Vector2(1016-HudPadding,HudContentTop-20),new Vector2(56,36));
            TopLeft(selectionPageLabel.rectTransform,new Vector2(1080-HudPadding,HudContentTop-24),new Vector2(80,28));
            TopLeft(nextSelectionPage.rect,new Vector2(1168-HudPadding,HudContentTop-20),new Vector2(56,36));
            selectionGridTitle.text=$"SELECTED · {members.Length} ITEMS · {groups.Length} TYPES";
            selectionPageLabel.text=$"{selectionPage+1} / {pages}";
            selectionPageLabel.gameObject.SetActive(pages>1);
            previousSelectionPage.rect.gameObject.SetActive(selectionPage>0);
            nextSelectionPage.rect.gameObject.SetActive(selectionPage+1<pages);
            for(int i=0;i<SelectionPageSize;i++)
            {
                var target=selectionTiles[i];target.rect.gameObject.SetActive(i<count);target.selectionType=null;
                if(i>=count)continue;
                TopLeft(target.rect,new Vector2((i%selectionLayoutColumns)*(tileWidth+HudGap),-(i/selectionLayoutColumns)*rowStride),new Vector2(tileWidth,tileHeight));
                LayoutSelectionTile(i,tileWidth,compact);
                var group=groups[selectionPage*SelectionPageSize+i];var type=Session.Catalog[group.Key];
                target.selectionType=group.Key;target.rect.name="Selection type "+group.Key;target.image.color=target.normal;
                selectionNames[i].text=type.name;selectionPortraits[i].SetType(type);
            }
            UpdateSelectionGrid();
        }
        private void LayoutSelectionTile(int index,float width,bool compact)
        {
            TopLeft(selectionPortraits[index].rectTransform,compact?new Vector2(8,-6):new Vector2(16,-44),compact?new Vector2(32,32):new Vector2(48,48));
            var name=selectionNames[index];name.fontSize=compact?16:20;
            TopLeft(name.rectTransform,compact?new Vector2(48,-18):new Vector2(76,-38),compact?new Vector2(width-56,38):new Vector2(width-92,60));
            var count=selectionCounts[index];count.fontSize=compact?16:20;count.alignment=compact?TextAnchor.MiddleLeft:TextAnchor.MiddleRight;
            var countSize=compact?new Vector2(36,20):new Vector2(54,26);
            TopLeft((RectTransform)count.transform.parent,compact?new Vector2(8,-38):new Vector2(width-70,-10),countSize);
            TopLeft(count.rectTransform,Vector2.zero,countSize);
            var energy=selectionEnergy[index];energy.fontSize=compact?16:18;energy.resizeTextMinSize=compact?14:16;energy.resizeTextMaxSize=compact?16:18;
            TopLeft(energy.rectTransform,compact?new Vector2(48,0):new Vector2(16,-10),compact?new Vector2(width-56,18):new Vector2(Mathf.Min(130,width-102),26));
            var healthPosition=compact?new Vector2(48,-58):new Vector2(16,-116);
            var healthSize=compact?new Vector2(width-56,2):new Vector2(width-32,4);
            TopLeft(selectionHealthTracks[index],healthPosition,healthSize);
            TopLeft(selectionHealthBars[index].rectTransform,healthPosition,healthSize);
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
                var type=Session.Catalog[tile.selectionType];
                float health=group.Sum(e=>e.health),maximum=type.health*group.Length;
                float ratio=maximum>0?Mathf.Clamp01(health/maximum):0;
                selectionCounts[i].text="×"+group.Length;
                selectionHealthBars[i].rectTransform.sizeDelta=new Vector2(selectionHealthTracks[i].rect.width*ratio,selectionHealthTracks[i].rect.height);
                selectionHealthBars[i].color=ratio<.3f?new Color(1,.3f,.2f):SupplyTint;
                var stat=selectionEnergy[i];stat.text="";
                if(type.energyMax>0)
                {
                    stat.text=$"{Mathf.FloorToInt(group.Sum(e=>e.energy))} / {Mathf.CeilToInt(type.energyMax*group.Length)}";
                    stat.color=new Color(.8f,.65f,1);
                }
                else
                {
                    var unfinished=type.building?group.Where(e=>!e.complete).ToArray():Array.Empty<VisibleEntity>();
                    if(unfinished.Length>0)
                    {
                        stat.text=$"{Mathf.Min(99,Mathf.FloorToInt(Mathf.Clamp01(unfinished.Average(e=>e.progress))*100))}%";
                        stat.color=WarningTint;
                    }
                    else if(health<maximum)
                    {
                        stat.text=$"{Mathf.CeilToInt(health)} / {Mathf.CeilToInt(maximum)}";
                        stat.color=ConsoleTheme.Muted;
                    }
                }
                stat.gameObject.SetActive(!string.IsNullOrEmpty(stat.text));
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
                ApplyTargetHover(tile,active);
                if(active)hint=Session.Catalog[tile.selectionType].name+" "+selectionCounts[i].text+" · "+(removing?"Remove this type from selection":hint);
            }
            selectionGridHint.text=hint;
        }
        private bool SelectionGestureValid()=>AcceptsGameInput&&!Quest.Placing&&!moving&&selectionGestureMatch==Session.SessionIdentity
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
                    selectionRemove=RemoveSelectionTypeHeld;gestureSelection=Session.Selection.ToArray();selectionGestureMatch=Session.SessionIdentity;
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
