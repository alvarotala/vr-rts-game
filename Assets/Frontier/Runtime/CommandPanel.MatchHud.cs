using TMPro;
using System;
using System.Globalization;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private RectTransform matchHud, resourceStrip, selectedCard, actionCard, groupStrip, matchStrip, harvestingStrip;
        private Text mineralValue, gasValue, supplyValue, supplyHint, selectedKind, selectionRole, selectionHealth, selectionDetails, casterStatus, actionHeading, actionHint, matchClock;
        private Image healthBackground, healthFill;
        private RectTransform orderPlanStrip;
        private Text orderPlanCount, orderPlanSteps, orderPlanHint;
        private RectTransform rallyPointStrip;
        private Text rallyPointValue, rallyPointHint;
        private readonly Text[] groupCounts=new Text[GameSession.ControlGroupCount];
        private readonly Target[] groupTargets=new Target[GameSession.ControlGroupCount], matchTargets=new Target[2];
        private static readonly Color MineralTint=new Color(.38f,.83f,1),GasTint=new Color(.85f,.62f,1),SupplyTint=new Color(.55f,.94f,.68f),WarningTint=new Color(1,.67f,.3f);

        private void CreateMatchHud()
        {
            matchHud=new GameObject("Match HUD",typeof(RectTransform)).GetComponent<RectTransform>();matchHud.SetParent(root,false);TopLeft(matchHud,Vector2.zero,root.sizeDelta);
            resourceStrip=Section(matchHud,"Resources",new Vector2(24,-72),new Vector2(1052,92),Color.clear);
            mineralValue=ResourceCard("MINERALS",0,MineralTint,out _);
            gasValue=ResourceCard("GAS",356,GasTint,out var gasHint);gasHint.text="CATALYST";
            supplyValue=ResourceCard("SUPPLY / CAPACITY",712,SupplyTint,out supplyHint);

            selectedCard=Section(matchHud,"Selected item",new Vector2(24,-178),new Vector2(320,296));
            selectedKind=Label(selectedCard,"SELECTION",new Vector2(16,-10),new Vector2(288,24),15,accent);
            // The large name is the shared selection label; navigation pages reuse it as their description.
            selectionRole=Label(selectedCard,"",new Vector2(16,-78),new Vector2(288,30),16,new Color(.72f,.82f,.87f));
            var bar=Section(selectedCard,"Health bar",new Vector2(16,-115),new Vector2(288,6),new Color(.12f,.22f,.25f));healthBackground=bar.GetComponent<Image>();
            healthFill=Section(bar,"Health",Vector2.zero,new Vector2(288,6),SupplyTint).GetComponent<Image>();
            selectionHealth=Label(selectedCard,"",new Vector2(16,-125),new Vector2(288,26),17,Color.white);
            selectionDetails=Label(selectedCard,"",new Vector2(16,-159),new Vector2(288,66),16,new Color(.72f,.82f,.87f),TextAlignmentOptions.TopLeft);
            casterStatus=Label(selectedCard,"",new Vector2(16,-228),new Vector2(288,32),14,new Color(.8f,.65f,1),TextAlignmentOptions.TopLeft);
            Label(selectedCard,"Hover an action to learn more",new Vector2(16,-262),new Vector2(288,24),14,accent);

            actionCard=Section(matchHud,"Selected item actions",new Vector2(360,-178),new Vector2(716,296));
            actionHeading=Label(actionCard,"ACTIONS",new Vector2(16,-10),new Vector2(684,24),15,accent);
            actionHint=Label(actionCard,"",new Vector2(36,-66),new Vector2(644,122),22,new Color(.65f,.76f,.82f),TextAlignmentOptions.Center);
            harvestingStrip=Section(matchHud,"Harvesting information",new Vector2(24,-422),new Vector2(1052,36));

            groupStrip=Section(matchHud,"Control groups",new Vector2(24,-470),new Vector2(716,100));
            Label(groupStrip,"CONTROL GROUPS",new Vector2(14,-7),new Vector2(200,24),15,accent);
            Label(groupStrip,"Tap to recall · Hold to save selection",new Vector2(222,-7),new Vector2(480,24),14,new Color(.65f,.76f,.82f),TextAlignmentOptions.Right);
            for(int g=0;g<GameSession.ControlGroupCount;g++)
            {
                int index=g;
                groupTargets[g]=HudButton(groupStrip,"Group "+(g+1),"Empty",new Vector2(14+g*174,-36),new Vector2(166,54),()=>Session.Group(index,false),()=>Session.Group(index,true),out groupCounts[g]);
            }
            matchStrip=Section(matchHud,"Match controls",new Vector2(756,-470),new Vector2(320,100));
            Label(matchStrip,"MATCH",new Vector2(14,-7),new Vector2(140,24),15,accent);
            matchClock=Label(matchStrip,"00:00",new Vector2(164,-7),new Vector2(142,24),17,Color.white,TextAlignmentOptions.Right);
            matchTargets[0]=HudButton(matchStrip,"Full map","Reset view",new Vector2(14,-36),new Vector2(140,54),()=>Board.Fit(),null,out _);
            matchTargets[1]=HudButton(matchStrip,"Pause","Menu",new Vector2(166,-36),new Vector2(140,54),()=>Show(MenuPage.Pause),null,out _);
            CreateProductionHud();
            CreateSelectionGrid();
            orderPlanStrip=Section(matchHud,"Unit order plan",new Vector2(24,-484),new Vector2(1052,96));
            Label(orderPlanStrip,"UNIT ORDER PLAN",new Vector2(16,-7),new Vector2(320,24),16,accent);
            orderPlanCount=Label(orderPlanStrip,"",new Vector2(350,-7),new Vector2(686,24),18,Color.white,TextAlignmentOptions.Right);
            orderPlanSteps=Label(orderPlanStrip,"",new Vector2(16,-34),new Vector2(1020,28),18,Color.white);
            orderPlanHint=Label(orderPlanStrip,"",new Vector2(16,-66),new Vector2(1020,23),14,new Color(.65f,.76f,.82f));
            rallyPointStrip=Section(matchHud,"Rally point",new Vector2(24,-484),new Vector2(1052,72));
            Label(rallyPointStrip,"RALLY POINT",new Vector2(16,-7),new Vector2(230,24),16,MineralTint);
            rallyPointValue=Label(rallyPointStrip,"",new Vector2(260,-7),new Vector2(776,24),18,Color.white,TextAlignmentOptions.Right);
            rallyPointHint=Label(rallyPointStrip,"",new Vector2(16,-38),new Vector2(1020,24),16,new Color(.65f,.76f,.82f));
        }
        private RectTransform Section(RectTransform parent,string name,Vector2 position,Vector2 size,Color? color=null)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(MenuSurface));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);TopLeft(rect,position,size);
            var image=go.GetComponent<MenuSurface>();image.color=color??MenuPalette.Surface;image.raycastTarget=false;
            if(Mathf.Min(size.x,size.y)<14){image.Radius=0;image.EdgeSoftness=0;}
            return rect;
        }
        private Text ResourceCard(string label,float x,Color tint,out Text hint)
        {
            var card=Section(resourceStrip,label,new Vector2(x,0),new Vector2(340,92));
            Icon(card,label=="MINERALS"?MenuIcon.Symbol.Minerals:label=="GAS"?MenuIcon.Symbol.Gas:MenuIcon.Symbol.Population,new Vector2(16,-13),22,tint);
            Label(card,label,new Vector2(47,-10),new Vector2(275,24),15,tint);
            var value=Label(card,"0",new Vector2(18,-35),new Vector2(194,45),36,MenuPalette.Ink);value.fontStyle=FontStyles.Bold;
            hint=Label(card,"",new Vector2(216,-36),new Vector2(108,44),14,tint,TextAlignmentOptions.Right);
            return value;
        }
        private SelectionPortrait ModelPortrait(RectTransform parent,Vector2 position,Vector2 size,Archetype model=null)
        {
            var portrait=new GameObject("Model portrait",typeof(RectTransform),typeof(SelectionPortrait)).GetComponent<SelectionPortrait>();
            portrait.transform.SetParent(parent,false);TopLeft(portrait.rectTransform,position,size);portrait.raycastTarget=false;
            if(model!=null)portrait.SetType(model);
            portrait.gameObject.SetActive(model!=null);return portrait;
        }
        private Target HudButton(RectTransform parent,string title,string subtitle,Vector2 position,Vector2 size,Action click,Action hold,out Text detail,Archetype model=null)
        {
            var rect=Section(parent,title,position,size,button);
            bool compact=size.y<54;
            var name=Label(rect,title,new Vector2(8,compact?-2:-3),new Vector2(size.x-16,compact?21:25),compact?16:18,Color.white,TextAlignmentOptions.Center);
            if(compact){name.enableAutoSizing=true;name.fontSizeMin=13;name.fontSizeMax=16;}
            detail=Label(rect,subtitle,new Vector2(8,compact?-23:-28),new Vector2(size.x-16,compact?18:21),compact?13:14,new Color(.71f,.83f,.87f),TextAlignmentOptions.Center);
            if(model!=null)
            {
                float portraitSize=Mathf.Min(44,size.y-10),left=portraitSize+16;
                ModelPortrait(rect,new Vector2(8,-5),new Vector2(portraitSize,portraitSize),model);
                name.text=model.name;name.alignment=detail.alignment=TextAlignmentOptions.Left;
                name.rectTransform.anchoredPosition=new Vector2(left,name.rectTransform.anchoredPosition.y);
                detail.rectTransform.anchoredPosition=new Vector2(left,detail.rectTransform.anchoredPosition.y);
                name.rectTransform.sizeDelta=new Vector2(size.x-left-8,name.rectTransform.sizeDelta.y);
                detail.rectTransform.sizeDelta=new Vector2(size.x-left-8,detail.rectTransform.sizeDelta.y);
            }
            return new Target{rect=rect,image=rect.GetComponent<Image>(),normal=button,click=click,hold=hold,detail=detail};
        }
        private void LayoutPage()
        {
            bool hud=Page==MenuPage.Actions,controls=Page==MenuPage.Hotkeys,guide=Page==MenuPage.Guide,reference=IsReferencePage,report=IsReportPage,history=Page==MenuPage.History,replay=Page==MenuPage.Replay;
            bool production=hud&&SelectedProducers().Length>0;
            bool orders=hud&&SelectedMobileUnits().Length>0;
            bool rally=hud&&OrderQueueVisuals.RallyProducers(Session).Length>0;
            float selectionSpace=LayoutSelectionGrid(hud);
            float queueSpace=selectionSpace+(production?180:0)+(orders?108:0)+(rally?84:0)+62;
            orderPlanStrip.gameObject.SetActive(orders);
            productionStrip.gameObject.SetActive(production);
            rallyPointStrip.gameObject.SetActive(rally);
            productionStrip.anchoredPosition=new Vector2(24,-484-selectionSpace);
            orderPlanStrip.anchoredPosition=new Vector2(24,-484-selectionSpace-(production?180:0));
            rallyPointStrip.anchoredPosition=new Vector2(24,-484-selectionSpace-(production?180:0)-(orders?108:0));
            matchHud.gameObject.SetActive(hud);root.sizeDelta=new Vector2(1100,hud?632+queueSpace:guide?920:controls?1004:reference||history?966:report?1028:replay?1082:690);matchHud.sizeDelta=root.sizeDelta;
            harvestingStrip.anchoredPosition=new Vector2(24,-422-queueSpace);
            groupStrip.anchoredPosition=new Vector2(24,-470-queueSpace);
            matchStrip.anchoredPosition=new Vector2(756,-470-queueSpace);
            matchTargets[0].rect.gameObject.SetActive(!Quest.Active);
            var pause=matchTargets[1].rect;
            pause.GetComponentInChildren<Text>().text=Session.IsOnline?"Menu":"Pause";
            TopLeft(pause,new Vector2(Quest.Active?14:166,-36),new Vector2(Quest.Active?292:140,54));
            foreach(RectTransform label in pause)label.sizeDelta=new Vector2(pause.sizeDelta.x-16,label.sizeDelta.y);
            headline.gameObject.SetActive(!hud);economy.gameObject.SetActive(!hud);
            selection.fontSize=hud?28:20;selection.color=hud?Color.white:new Color(.65f,.76f,.82f);
            TopLeft(selection.rectTransform,hud?new Vector2(40,-211):new Vector2(30,-169),hud?new Vector2(288,42):new Vector2(1040,38));
            harvesting.fontSize=hud?17:18;
            TopLeft(harvesting.rectTransform,hud?new Vector2(40,-425-queueSpace):new Vector2(30,-207),hud?new Vector2(1020,30):new Vector2(1040,28));
            status.fontSize=replay?16:hud?18:20;
            TopLeft(statusBanner,hud?new Vector2(24,-581-queueSpace):new Vector2(30,guide?-860:controls?-938:reference||history?-910:report?-972:replay?-1020:-638),new Vector2(hud?1052:1040,48));
            TopLeft(actions,hud?new Vector2(376,-218):new Vector2(30,guide?-786:controls?-866:reference?-840:-240),hud?new Vector2(684,240):new Vector2(1040,report?716:history?648:replay?756:controls||guide||reference?58:396));
            LayoutBrand();
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
            for(int g=0;g<GameSession.ControlGroupCount;g++){int count=Session.GroupSize(g);groupCounts[g].text=count==0?"Empty":count+" assigned";}

            var selected=view.entities.Where(e=>Session.Selection.Contains(e.id)).ToArray();
            selectedKind.text="SELECTION";selectionRole.text="";selectionHealth.text="";selectionDetails.text="";casterStatus.text="";
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
                if(types.Length>5)((LocalizedText)selectionDetails).Append($"\n+ {types.Skip(5).Sum(g=>g.Count())} other items");
                TopLeft(selectionDetails.rectTransform,new Vector2(16,-107),new Vector2(288,118));
            }
            else
            {
                var deposit=view.resources.FirstOrDefault(r=>r.id==(Session.SelectedResource>0?Session.SelectedResource:Board.HoveredResource));
                var pointed=view.entities.FirstOrDefault(e=>e.id==Board.Hovered&&!e.remembered);
                if(deposit!=null)
                {
                    selectedKind.text=Session.SelectedResource>0?"RESOURCE DEPOSIT":"POINTING AT · RESOURCE";selection.text=deposit.kind=="mineral"?"Minerals":"Gas";
                    selectionRole.text=deposit.kind=="mineral"?"MINERAL PATCH":"CATALYST DEPOSIT";
                    selectionDetails.text=deposit.remaining<0?"Remaining amount unknown":$"{deposit.remaining:N0} remaining";
                }
                else if(pointed!=null)DescribeEntity(pointed,true);
                else{selection.text="No selection";selectionDetails.text="Select a unit or building to see its name and available orders.";}
            }
            UpdateCasterStatus(selected);
            UpdateMatchActionDetails();
            UpdateHarvesting(selected);harvestingStrip.gameObject.SetActive(!string.IsNullOrEmpty(harvesting.text));
            UpdateProductionHud();
            UpdateSelectionGrid();
            UpdateOrderPlanHud();
            UpdateRallyPointHud();
            status.text=Session.IsPaused?(Session.IsOnline?"INPUT UNAVAILABLE · The online match continues.":"MATCH PAUSED · Waiting for headset, focus or tracking to resume."):Session.Message;
            UpdateStatusStyle(!Session.IsPaused&&Session.MessageIsError);
        }
        private void UpdateCasterStatus(VisibleEntity[] selected)
        {
            var casters=selected.Where(e=>e.owner==Session.View.player&&!e.remembered&&Session.Catalog[e.definition].energyMax>0).ToArray();
            if(casters.Length==0)return;
            float energy=casters.Sum(e=>e.energy),capacity=casters.Sum(e=>Session.Catalog[e.definition].energyMax);
            int ready=0,total=0;
            foreach(var caster in casters)foreach(string id in Session.Catalog[caster.definition].abilities)
            {
                var ability=Session.Catalog.Ability(id);if(ability==null)continue;total++;
                if(caster.complete&&caster.health>0&&caster.energy>=ability.energyCost&&!caster.abilityCooldowns.Any(c=>c.ability==id&&c.readyTick>Session.View.tick))ready++;
            }
            casterStatus.text=$"ENERGY {Mathf.FloorToInt(energy)} / {Mathf.CeilToInt(capacity)}"+(casters.Length>1?$" · {casters.Length} casters":"")+$"\n{ready} / {total} abilities ready";
        }
        private void UpdateMatchActionDetails()
        {
            // Cooldowns change every tick. Refresh labels without rebuilding buttons,
            // which would invalidate a press that is still held on the console.
            var available=SelectionActions.For(Session);
            actionHeading.text=Session.BuildDefinition!=null?"PLACING · "+Session.Catalog[Session.BuildDefinition].name
                :Session.AbilityDefinitionId!=null?"TARGET · "+Session.PendingAbility.name:available.Heading;
            foreach(var target in targets.Where(t=>t.actionId!=null))
            {
                var action=available.Items.FirstOrDefault(a=>a.Id==target.actionId);if(action==null)continue;
                bool active=action.Building&&action.Id=="build:"+Session.BuildDefinition;
                target.help=action;target.detail.text=MatchActionDetail(action,active);
            }
        }
        private VisibleEntity[] SelectedMobileUnits()=>Session.View==null?Array.Empty<VisibleEntity>():Session.View.entities
            .Where(e=>e.owner==Session.View.player&&!e.remembered&&Session.Selection.Contains(e.id)&&!Session.Catalog[e.definition].building).OrderBy(e=>e.id).ToArray();
        private void UpdateRallyPointHud()
        {
            var producers=OrderQueueVisuals.RallyProducers(Session);if(producers.Length==0)return;
            if(producers.Length==1)
            {
                var producer=producers[0];
                rallyPointValue.text=producer.hasRallyPoint?string.Format(CultureInfo.InvariantCulture,"SET · {0:0.#}, {1:0.#}",producer.rallyPoint.x,producer.rallyPoint.y):"DEFAULT · No rally point";
            }
            else rallyPointValue.text=$"{producers.Count(e=>e.hasRallyPoint)} / {producers.Length} rally points set";
            rallyPointHint.text=Session.CanSetRallyPoint?"A on terrain: set rally point · Newly trained units move to the cyan flag":"Select only production buildings to set rally points · Cyan flag: new-unit destination";
        }
        private void UpdateOrderPlanHud()
        {
            var units=SelectedMobileUnits();if(units.Length==0)return;
            int count=units.Sum(e=>e.pendingOrders.Length),withOrders=units.Count(e=>e.pendingOrders.Length>0);
            orderPlanCount.text=units.Length==1?$"{count} queued order{(count==1?"":"s")}":$"{count} queued orders across {withOrders} / {units.Length} units";
            var representative=OrderQueueVisuals.Representative(Session);
            if(representative==null)
            {
                orderPlanSteps.text="No queued orders";
                orderPlanHint.text="Hold right grip + A to append orders · Double A: Attack Move / attack target";
                return;
            }
            orderPlanSteps.text="1. "+PlanOrderLabel(representative.order)+"  →  "+string.Join("  →  ",representative.pendingOrders.Take(3).Select((order,index)=>(index+2)+". "+PlanOrderLabel(order.kind)));
            if(representative.pendingOrders.Length>3)((LocalizedText)orderPlanSteps).Append($"  →  +{representative.pendingOrders.Length-3} more");
            orderPlanHint.text=$"Waypoints: {Session.Catalog[representative.definition].name} #{representative.id} · Cyan: Move · Red: Attack · Green: Work";
            if(representative.pendingOrders.Length>=OrderQueueVisuals.MaximumWaypoints)((LocalizedText)orderPlanHint).Append($" · First {OrderQueueVisuals.MaximumWaypoints} shown");
        }
        private static string PlanOrderLabel(CommandKind order)
        {
            switch(order){case CommandKind.Move:return "Move";case CommandKind.Attack:return "Attack";case CommandKind.AttackMove:return "Attack Move";case CommandKind.Gather:return "Gather";case CommandKind.Build:return "Build";case CommandKind.Repair:return "Repair";case CommandKind.Hold:return "Hold Position";case CommandKind.Cast:return "Cast";default:return "Stop";}
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
            if(!entity.complete)selectionDetails.text=$"UNDER CONSTRUCTION · {Mathf.Min(99,Mathf.FloorToInt(entity.progress*100))}%\n"+
                (type.role=="hq"?"Cannot receive resources yet": "Not operational yet");
            else if(entity.queue.Length>0)
            {
                selectionDetails.text=pointing?$"{entity.queue.Length} queued\nSelect to view production":$"1 active · {entity.queue.Length-1} waiting\nSee production queue below";
            }
            else selectionDetails.text=type.building?"Ready":OrderLabel(entity.order);
        }
        private static string BuildingRole(string role)
        {
            switch(role){case "hq":return "Headquarters";case "extractor":return "Gas extractor";case "supply":return "Supply";case "barracks":return "Production";case "tech":return "Technology";case "academy":return "Caster production";case "defense":return "Defense";default:return "Building";}
        }
        private static string OrderLabel(CommandKind order)
        {
            switch(order){case CommandKind.Move:return "Moving";case CommandKind.Attack:return "Attacking";case CommandKind.AttackMove:return "Attack move";case CommandKind.Gather:return "Gathering resources";case CommandKind.Build:return "Building";case CommandKind.Repair:return "Repairing";case CommandKind.Hold:return "Holding position";case CommandKind.Cast:return "Casting ability";default:return "Idle";}
        }
        private void BuildMatchActions()
        {
            targets.AddRange(groupTargets);targets.AddRange(matchTargets.Where(t=>t.rect.gameObject.activeSelf));
            BindProductionTargets();
            BindSelectionTargets();
            var available=SelectionActions.For(Session);
            actionHint.text=available.Hint;actionHeading.text=available.Heading;
            if(Session.BuildDefinition!=null)actionHeading.text="PLACING · "+Session.Catalog[Session.BuildDefinition].name;
            var groups=available.Items.GroupBy(a=>a.Category).ToArray();
            int rows=groups.Sum(g=>(g.Count()+2)/3);
            float rowHeight=rows==0?60:Mathf.Min(60,(actions.rect.height-groups.Length*20)/rows);
            // Four rows of mixed producer actions still need room for both text lines.
            // Reduce only their spacing; ordinary 54-pixel cards keep their layout.
            float buttonHeight=Mathf.Min(rowHeight-2,Mathf.Max(42,rowHeight-6)),y=0;
            foreach(var group in groups)
            {
                var tint=ActionTint(group.Key);
                Label(actions,CategoryLabel(group.Key),new Vector2(8,-y),new Vector2(660,18),13,tint);
                y+=18;int column=0;
                foreach(var action in group)
                {
                    var captured=action;
                    bool active=action.Building&&action.Id=="build:"+Session.BuildDefinition;
                    var target=HudButton(actions,action.Title,MatchActionDetail(action,active),new Vector2(column*230,-y),new Vector2(218,buttonHeight),()=>captured.Execute(),null,out var detail,action.Model);
                    target.normal=Color.Lerp(button,tint,active ? .3f : .065f);target.image.color=target.normal;target.help=action;target.actionId=action.Id;
                    if(active)
                    {
                        detail.color=Color.white;detail.fontStyle=FontStyles.Bold;
                    }
                    Section(target.rect,"Category accent",new Vector2(4,-10),new Vector2(4,buttonHeight-20),tint);
                    targets.Add(target);actionIndex++;
                    if(++column==3){column=0;y+=rowHeight;}
                }
                if(column!=0)y+=rowHeight;
                y+=2;
            }
            actionHint.gameObject.SetActive(actionIndex==0);
        }
        private static string MatchActionDetail(SelectionAction action,bool active)
        {
            // The category already says Build / Train; leave room for the model
            // and cost. Roles and requirements remain in the action's hover help.
            var model=action.Model;
            string detail=model==null?action.Detail:model.minerals+"M"+(model.catalyst>0?" / "+model.catalyst+"G":"");
            return (active?"SELECTED · ":"")+detail;
        }
    }
}
