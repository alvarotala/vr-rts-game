using System;
using System.Globalization;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private RectTransform matchHud, resourceStrip, selectedCard, actionCard, groupStrip, matchStrip, harvestingStrip;
        private Text mineralValue, gasValue, supplyValue, supplyHint, selectedKind, selectionRole, selectionHealth, selectionDetails, actionHeading, actionHint, matchClock;
        private Image healthBackground, healthFill;
        private readonly Text[] groupCounts=new Text[3];
        private readonly Target[] groupTargets=new Target[3], matchTargets=new Target[2];
        private static readonly Color MineralTint=new Color(.38f,.83f,1),GasTint=new Color(.85f,.62f,1),SupplyTint=new Color(.55f,.94f,.68f),WarningTint=new Color(1,.67f,.3f);

        private void CreateMatchHud()
        {
            matchHud=new GameObject("Match HUD",typeof(RectTransform)).GetComponent<RectTransform>();matchHud.SetParent(root,false);TopLeft(matchHud,Vector2.zero,root.sizeDelta);
            resourceStrip=Section(matchHud,"Resources",new Vector2(24,-72),new Vector2(1052,92),Color.clear);
            mineralValue=ResourceCard("MINERALS",0,MineralTint,out _);
            gasValue=ResourceCard("GAS",356,GasTint,out var gasHint);gasHint.text="CATALYST";
            supplyValue=ResourceCard("SUPPLY / CAPACITY",712,SupplyTint,out supplyHint);

            selectedCard=Section(matchHud,"Selected item",new Vector2(24,-178),new Vector2(320,234));
            selectedKind=Label(selectedCard,"SELECTION",new Vector2(16,-10),new Vector2(288,24),15,accent);
            // The large name is the shared selection label; navigation pages reuse it as their description.
            selectionRole=Label(selectedCard,"",new Vector2(16,-78),new Vector2(288,30),16,new Color(.72f,.82f,.87f));
            var bar=Section(selectedCard,"Health bar",new Vector2(16,-115),new Vector2(288,6),new Color(.12f,.22f,.25f));healthBackground=bar.GetComponent<Image>();
            healthFill=Section(bar,"Health",Vector2.zero,new Vector2(288,6),SupplyTint).GetComponent<Image>();
            selectionHealth=Label(selectedCard,"",new Vector2(16,-125),new Vector2(288,26),17,Color.white);
            selectionDetails=Label(selectedCard,"",new Vector2(16,-159),new Vector2(288,66),16,new Color(.72f,.82f,.87f),TextAnchor.UpperLeft);

            actionCard=Section(matchHud,"Selected item actions",new Vector2(360,-178),new Vector2(716,234));
            actionHeading=Label(actionCard,"ACTIONS",new Vector2(16,-10),new Vector2(684,24),15,accent);
            actionHint=Label(actionCard,"",new Vector2(36,-66),new Vector2(644,122),22,new Color(.65f,.76f,.82f),TextAnchor.MiddleCenter);
            harvestingStrip=Section(matchHud,"Harvesting information",new Vector2(24,-422),new Vector2(1052,36));

            groupStrip=Section(matchHud,"Control groups",new Vector2(24,-470),new Vector2(716,100));
            Label(groupStrip,"CONTROL GROUPS",new Vector2(14,-7),new Vector2(200,24),15,accent);
            Label(groupStrip,"Tap to recall · Hold to save selection",new Vector2(222,-7),new Vector2(480,24),14,new Color(.65f,.76f,.82f),TextAnchor.MiddleRight);
            for(int g=0;g<3;g++)
            {
                int index=g;
                groupTargets[g]=HudButton(groupStrip,"Group "+(g+1),"Empty",new Vector2(14+g*230,-36),new Vector2(218,54),()=>Session.Group(index,false),()=>Session.Group(index,true),out groupCounts[g]);
            }
            matchStrip=Section(matchHud,"Match controls",new Vector2(756,-470),new Vector2(320,100));
            Label(matchStrip,"MATCH",new Vector2(14,-7),new Vector2(140,24),15,accent);
            matchClock=Label(matchStrip,"00:00",new Vector2(164,-7),new Vector2(142,24),17,Color.white,TextAnchor.MiddleRight);
            matchTargets[0]=HudButton(matchStrip,"Full map","Reset view",new Vector2(14,-36),new Vector2(140,54),()=>Board.Fit(),null,out _);
            matchTargets[1]=HudButton(matchStrip,"Pause","Menu",new Vector2(166,-36),new Vector2(140,54),()=>Show(MenuPage.Pause),null,out _);
            CreateProductionHud();
        }
        private RectTransform Section(RectTransform parent,string name,Vector2 position,Vector2 size,Color? color=null)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);TopLeft(rect,position,size);
            var image=go.GetComponent<Image>();image.color=color??new Color(.04f,.095f,.13f,.98f);image.raycastTarget=false;return rect;
        }
        private Text ResourceCard(string label,float x,Color tint,out Text hint)
        {
            var card=Section(resourceStrip,label,new Vector2(x,0),new Vector2(340,92));
            Section(card,"Accent",Vector2.zero,new Vector2(4,92),tint);
            Label(card,label,new Vector2(18,-8),new Vector2(304,24),16,tint);
            var value=Label(card,"0",new Vector2(16,-32),new Vector2(210,50),38,Color.white);
            hint=Label(card,"",new Vector2(216,-36),new Vector2(108,44),14,tint,TextAnchor.MiddleRight);
            return value;
        }
        private Target HudButton(RectTransform parent,string title,string subtitle,Vector2 position,Vector2 size,Action click,Action hold,out Text detail)
        {
            var rect=Section(parent,title,position,size,button);
            Label(rect,title,new Vector2(8,-3),new Vector2(size.x-16,25),18,Color.white,TextAnchor.MiddleCenter);
            detail=Label(rect,subtitle,new Vector2(8,-28),new Vector2(size.x-16,21),14,new Color(.71f,.83f,.87f),TextAnchor.MiddleCenter);
            return new Target{rect=rect,image=rect.GetComponent<Image>(),normal=button,click=click,hold=hold};
        }
        private void LayoutPage()
        {
            bool hud=Page==MenuPage.Actions,controls=Page==MenuPage.Controls;
            bool production=hud&&SelectedProducer()!=null;
            float queueSpace=production?180:0;
            productionStrip.gameObject.SetActive(production);
            matchHud.gameObject.SetActive(hud);root.sizeDelta=new Vector2(1100,hud?632+queueSpace:526);matchHud.sizeDelta=root.sizeDelta;
            harvestingStrip.anchoredPosition=new Vector2(24,-422-queueSpace);
            groupStrip.anchoredPosition=new Vector2(24,-470-queueSpace);
            matchStrip.anchoredPosition=new Vector2(756,-470-queueSpace);
            matchTargets[0].rect.gameObject.SetActive(!Quest.Active);
            var pause=matchTargets[1].rect;
            TopLeft(pause,new Vector2(Quest.Active?14:166,-36),new Vector2(Quest.Active?292:140,54));
            foreach(RectTransform label in pause)label.sizeDelta=new Vector2(pause.sizeDelta.x-16,label.sizeDelta.y);
            headline.gameObject.SetActive(!hud);economy.gameObject.SetActive(!hud);
            selection.fontSize=hud?28:20;selection.color=hud?Color.white:new Color(.65f,.76f,.82f);
            TopLeft(selection.rectTransform,hud?new Vector2(40,-211):new Vector2(30,-169),hud?new Vector2(288,42):new Vector2(1040,controls?225:38));
            harvesting.fontSize=hud?17:18;
            TopLeft(harvesting.rectTransform,hud?new Vector2(40,-425-queueSpace):new Vector2(30,-207),hud?new Vector2(1020,30):new Vector2(1040,28));
            status.fontSize=hud?18:20;
            TopLeft(status.rectTransform,hud?new Vector2(24,-581-queueSpace):new Vector2(30,-473),hud?new Vector2(1052,40):new Vector2(1040,44));
            TopLeft(actions,hud?new Vector2(376,-218):new Vector2(30,controls?-411:-240),hud?new Vector2(684,178):new Vector2(1040,controls?58:225));
        }
        private void UpdateMatchHud()
        {
            var view=Session.View;
            mineralValue.text=view.minerals.ToString("N0",CultureInfo.InvariantCulture);gasValue.text=view.catalyst.ToString("N0",CultureInfo.InvariantCulture);
            supplyValue.text=$"{view.supply} / {view.capacity}";
            bool full=view.supply>=view.capacity;
            supplyValue.color=full?WarningTint:Color.white;supplyHint.color=full?WarningTint:SupplyTint;
            supplyHint.text=full?"FULL":$"{view.capacity-view.supply} available";
            matchClock.text=$"{view.tick/1200:00}:{view.tick/20%60:00}";
            for(int g=0;g<3;g++){int count=Session.GroupSize(g);groupCounts[g].text=count==0?"Empty":count+" assigned";}

            var selected=view.entities.Where(e=>Session.Selection.Contains(e.id)).ToArray();
            selectedKind.text="SELECTION";selectionRole.text="";selectionHealth.text="";selectionDetails.text="";
            healthBackground.gameObject.SetActive(false);
            TopLeft(selectionDetails.rectTransform,new Vector2(16,-159),new Vector2(288,66));
            if(selected.Length==1)DescribeEntity(selected[0],false);
            else if(selected.Length>1)
            {
                selectedKind.text="MULTIPLE SELECTION";
                selection.text=selected.Length+(selected.All(e=>!Session.Catalog[e.definition].building)?" units":selected.All(e=>Session.Catalog[e.definition].building)?" buildings":" items");
                selectionRole.text="SELECTED COMPOSITION";
                var types=selected.GroupBy(e=>e.definition).OrderByDescending(g=>g.Count()).ToArray();
                selectionDetails.text=string.Join("\n",types.Take(5).Select(g=>$"{g.Count()} × {Session.Catalog[g.Key].name}"));
                if(types.Length>5)selectionDetails.text+=$"\n+ {types.Skip(5).Sum(g=>g.Count())} other items";
                TopLeft(selectionDetails.rectTransform,new Vector2(16,-107),new Vector2(288,118));
            }
            else
            {
                var deposit=view.resources.FirstOrDefault(r=>r.id==Session.SelectedResource);
                var pointed=view.entities.FirstOrDefault(e=>e.id==Board.Hovered&&!e.remembered);
                if(deposit!=null)
                {
                    selectedKind.text="RESOURCE DEPOSIT";selection.text=deposit.kind=="mineral"?"Minerals":"Gas";
                    selectionRole.text=deposit.kind=="mineral"?"MINERAL PATCH":"CATALYST DEPOSIT";
                    selectionDetails.text=deposit.remaining<0?"Remaining amount unknown":$"{deposit.remaining:N0} remaining";
                }
                else if(pointed!=null)DescribeEntity(pointed,true);
                else{selection.text="No selection";selectionDetails.text="Select a unit or building to see its name and available orders.";}
            }
            UpdateHarvesting(selected);harvestingStrip.gameObject.SetActive(!string.IsNullOrEmpty(harvesting.text));
            UpdateProductionHud();
            status.text=Session.IsPaused?"MATCH PAUSED · Waiting for headset, focus or tracking to resume.":Session.Message;
        }
        private void DescribeEntity(VisibleEntity entity,bool pointing)
        {
            var type=Session.Catalog[entity.definition];
            selectedKind.text=pointing?"POINTING AT · "+(entity.owner==Session.View.player?"ALLY":"ENEMY"):type.building?"SELECTED BUILDING":"SELECTED UNIT";
            selection.text=type.name;
            selectionRole.text=(type.building?BuildingRole(type.role):UnitVisuals.RoleName(type.role))+" · "+ShortFaction(type.faction);
            float ratio=Mathf.Clamp01(entity.health/type.health);healthBackground.gameObject.SetActive(true);
            healthFill.rectTransform.sizeDelta=new Vector2(288*ratio,6);healthFill.color=ratio<.3f?WarningTint:SupplyTint;
            selectionHealth.text=$"{Mathf.CeilToInt(entity.health)} / {Mathf.CeilToInt(type.health)} HP";
            if(!entity.complete)selectionDetails.text=$"Construction {entity.progress*100:0}%";
            else if(entity.queue.Length>0)
            {
                selectionDetails.text=pointing?$"{entity.queue.Length} queued\nSelect to view production":$"1 active · {entity.queue.Length-1} waiting\nSee production queue below";
            }
            else selectionDetails.text=type.building?"Ready":OrderLabel(entity.order);
        }
        private static string BuildingRole(string role)
        {
            switch(role){case "hq":return "Headquarters";case "extractor":return "Gas extractor";case "supply":return "Supply";case "barracks":return "Production";case "tech":return "Technology";case "defense":return "Defense";default:return "Building";}
        }
        private static string OrderLabel(CommandKind order)
        {
            switch(order){case CommandKind.Move:return "Moving";case CommandKind.Attack:return "Attacking";case CommandKind.AttackMove:return "Attack move";case CommandKind.Gather:return "Gathering resources";case CommandKind.Build:return "Building";case CommandKind.Repair:return "Repairing";case CommandKind.Hold:return "Holding position";default:return "Idle";}
        }
        private void BuildMatchActions()
        {
            targets.AddRange(groupTargets);targets.AddRange(matchTargets.Where(t=>t.rect.gameObject.activeSelf));
            var available=SelectionActions.For(Session);
            actionHint.text=available.Hint;actionHeading.text=available.Heading;
            foreach(var action in available.Items)
            {var captured=action;AddMatchAction(action.Title,action.Detail,()=>captured.Execute());}
            actionHint.gameObject.SetActive(actionIndex==0);
        }
        private void AddMatchAction(string title,string detail,Action click)
        {
            int n=actionIndex++;
            targets.Add(HudButton(actions,title,detail,new Vector2((n%3)*230,-(n/3)*62),new Vector2(218,54),click,null,out _));
        }
    }
}
