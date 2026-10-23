using System;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const int SelectionCapacity=Simulation.UnitLimit+Simulation.BuildingLimit,SelectionColumns=6,CompactSelectionColumns=7;
        private RectTransform selectionGrid;
        private Text selectionGridHint;
        private readonly Target[] selectionTiles=new Target[SelectionCapacity];
        private readonly SelectionPortrait[] selectionPortraits=new SelectionPortrait[SelectionCapacity];
        private readonly Text[] selectionNames=new Text[SelectionCapacity],selectionCounts=new Text[SelectionCapacity];
        private readonly Text[] selectionEnergy=new Text[SelectionCapacity];
        private readonly Image[] selectionHealthBars=new Image[SelectionCapacity];
        private readonly RectTransform[] selectionHealthTracks=new RectTransform[SelectionCapacity];
        private int selectionLayoutColumns=SelectionColumns;
        private bool selectionIndividuals,selectionPortraitOnly;
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
            for(int i=0;i<SelectionCapacity;i++)
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
            if(roster!=selectionRoster){selectionRoster=roster;ClearSelectionGesture();}
            bool show=hud&&members.Length>0;selectionGrid.gameObject.SetActive(show);if(!show)return;
            var groups=members.GroupBy(e=>e.definition).ToArray();
            selectionIndividuals=groups.Length==1;
            int count=selectionIndividuals?members.Length:groups.Length;
            bool compact=count>SelectionColumns;
            selectionPortraitOnly=count>CompactSelectionColumns*2;
            selectionLayoutColumns=selectionPortraitOnly?(count+1)/2:compact?CompactSelectionColumns:SelectionColumns;
            float tileHeight=compact?60:136,width=HudInnerWidth-HudPadding*2;
            float gap=selectionPortraitOnly?6:HudGap;
            float tileWidth=(width-(selectionLayoutColumns-1)*gap)/selectionLayoutColumns,rowStride=tileHeight+HudGap;
            selectionGrid.SetParent(selectedCard,false);
            TopLeft(selectionGrid,new Vector2(HudPadding,-HudContentTop),new Vector2(width,136));
            for(int i=0;i<SelectionCapacity;i++)
            {
                var target=selectionTiles[i];target.rect.gameObject.SetActive(i<count);target.selectionType=null;target.selectionEntity=0;
                if(i>=count)continue;
                TopLeft(target.rect,new Vector2((i%selectionLayoutColumns)*(tileWidth+gap),-(i/selectionLayoutColumns)*rowStride),new Vector2(tileWidth,tileHeight));
                LayoutSelectionTile(i,tileWidth,compact);
                string definition=selectionIndividuals?members[i].definition:groups[i].Key;var type=Session.Catalog[definition];
                target.selectionType=definition;target.selectionEntity=selectionIndividuals?members[i].id:0;
                target.rect.name=selectionIndividuals?"Selection entity "+target.selectionEntity:"Selection type "+definition;target.image.color=target.normal;
                selectionNames[i].text=type.name;selectionPortraits[i].SetType(type);
            }
            UpdateSelectionGrid();
        }
        private void LayoutSelectionTile(int index,float width,bool compact)
        {
            selectionNames[index].gameObject.SetActive(!selectionPortraitOnly);
            selectionCounts[index].transform.parent.gameObject.SetActive(!selectionIndividuals);
            // Large single-type armies keep every individual selectable in the same
            // two rows. Their model and own health bar replace repeated tiny labels.
            if(selectionPortraitOnly)
            {
                float portraitSize=Mathf.Min(40,width-8);
                TopLeft(selectionPortraits[index].rectTransform,new Vector2((width-portraitSize)*.5f,-(48-portraitSize)*.5f),new Vector2(portraitSize,portraitSize));
                TopLeft(selectionHealthTracks[index],new Vector2(6,-52),new Vector2(width-12,4));
                TopLeft(selectionHealthBars[index].rectTransform,new Vector2(6,-52),new Vector2(width-12,4));
                return;
            }
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
        }
        private void UpdateSelectionGrid()
        {
            if(!selectionGrid.gameObject.activeInHierarchy)return;
            var members=SelectionMembers();
            for(int i=0;i<SelectionCapacity;i++)
            {
                var tile=selectionTiles[i];if(tile.selectionType==null)continue;
                var group=members.Where(e=>tile.selectionEntity>0?e.id==tile.selectionEntity:e.definition==tile.selectionType).ToArray();
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
                stat.gameObject.SetActive(!selectionPortraitOnly&&!string.IsNullOrEmpty(stat.text));
            }
            UpdateSelectionHover();
        }
        // Panel gestures use fixed navigation inputs, independent of map hotkeys.
        private bool RemoveSelectionTypeHeld=>HotkeyInput.Read(Quest.Active?"touch:grip":"key:LeftShift",Quest.ActiveController).Held;
        private void UpdateSelectionHover()
        {
            string hint=selectionIndividuals
                ?(Quest.Active?"Trigger / "+Bindings.MenuConfirmName+": select individual · Grip + Trigger: remove individual":"Click: select individual · Shift + click: remove individual")
                :(Quest.Active?"Trigger / "+Bindings.MenuConfirmName+": select type · Grip + Trigger: remove type":"Click: select type · Shift + click: remove type");
            for(int i=0;i<SelectionCapacity;i++)
            {
                var tile=selectionTiles[i];if(tile.selectionType==null)continue;
                bool active=hovered==tile;
                bool removing=pressed==tile?selectionRemove:RemoveSelectionTypeHeld;
                ApplyTargetHover(tile,active);
                if(active)hint=Session.Catalog[tile.selectionType].name+(selectionIndividuals?"":" "+selectionCounts[i].text)+" · "+
                    (removing?(selectionIndividuals?"Remove this individual from selection":"Remove this type from selection"):hint);
            }
            selectionGridHint.text="One type: each card is an individual. Different types: each card summarizes a type."+"\n\n"+hint;
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
                    string type=target.selectionType;int entity=target.selectionEntity;bool remove=selectionRemove;ClearSelectionGesture();
                    if(entity>0){if(remove)Session.Deselect(entity);else Session.Select(entity,false);}
                    else Session.SelectTypeInSelection(type,remove);
                    nextText=0;
                }
                UpdateSelectionHover();return true;
            }
            UpdateSelectionHover();return false;
        }
    }
}
