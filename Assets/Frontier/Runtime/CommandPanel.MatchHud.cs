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
        private const float HudWidth=1480,HudInnerWidth=1416,HudBodyTop=188,HudSelectionHeight=236;
        private const float HudPadding=24,HudContentTop=76,HudGap=16,HudTitleSize=20;
        private const float HudActionWidth=328,HudActionHeight=112,HudActionStride=128;
        // Preserve the previous physical window size at its default scale.
        private const float HudWorldScale=1512f/1636f;
        private RectTransform matchHud, resourceStrip, selectedCard, actionCard, groupStrip, matchStrip, harvestingStrip;
        private Text mineralValue, gasValue, supplyValue, supplyHint, selectedKind, selectionRole, selectionHealth, selectionDetails, casterStatus, actionHeading, actionHint, matchClock, matchMap, matchInfo, optionsTitle, selectionFallbackTitle;
        private Image healthBackground, healthFill;
        private SelectionPortrait selectedPortrait;
        private RectTransform orderPlanStrip;
        private Text orderPlanTitle, orderPlanCount, orderPlanHint;
        private RectTransform rallyPointStrip;
        private Text rallyPointValue, rallyPointHint;
        private readonly Text[] groupCounts=new Text[GameSession.ControlGroupCount];
        private readonly Target[] groupTargets=new Target[GameSession.ControlGroupCount], matchTargets=new Target[2];
        private static readonly Color MineralTint=new Color(.38f,.83f,1),GasTint=new Color(.85f,.62f,1),SupplyTint=new Color(.55f,.94f,.68f),WarningTint=new Color(1,.67f,.3f);

        private void CreateMatchHud()
        {
            matchHud=new GameObject("Match HUD",typeof(RectTransform)).GetComponent<RectTransform>();matchHud.SetParent(root,false);TopLeft(matchHud,Vector2.zero,root.sizeDelta);
            resourceStrip=Section(matchHud,"Resources",new Vector2(32,-44),new Vector2(HudInnerWidth,100),Color.clear);
            FlatHud(resourceStrip);
            mineralValue=ResourceCard("MINERALS",0,MineralTint,ConsoleGlyph.Kind.Mineral,out _);
            gasValue=ResourceCard("GAS",368,GasTint,ConsoleGlyph.Kind.Gas,out _);
            supplyValue=ResourceCard("POPULATION",736,SupplyTint,ConsoleGlyph.Kind.Supply,out supplyHint);
            supplyHint.gameObject.SetActive(false);

            selectedCard=Section(matchHud,"Selected item",new Vector2(32,-HudBodyTop),new Vector2(HudInnerWidth,HudSelectionHeight));
            selectedKind=HudTitle(selectedCard,"SELECTION");
            selectionFallbackTitle=Label(selectedCard,"",new Vector2(112,-HudContentTop),new Vector2(560,32),24,ConsoleTheme.Ink);selectionFallbackTitle.fontStyle=FontStyle.Bold;
            selectionRole=Label(selectedCard,"",new Vector2(20,-78),new Vector2(284,26),17,ConsoleTheme.Muted);
            var bar=Section(selectedCard,"Health bar",new Vector2(20,-112),new Vector2(264,6),ConsoleTheme.Track);healthBackground=bar.GetComponent<Image>();
            healthFill=Section(bar,"Health",Vector2.zero,new Vector2(264,6),SupplyTint).GetComponent<Image>();
            selectionHealth=Label(selectedCard,"",new Vector2(20,-124),new Vector2(264,28),18,ConsoleTheme.Ink);selectionHealth.fontStyle=FontStyle.Bold;
            selectionDetails=Label(selectedCard,"",new Vector2(492,-32),new Vector2(700,58),19,ConsoleTheme.Muted,TextAnchor.UpperLeft);
            selectedPortrait=ModelPortrait(selectedCard,new Vector2(328,-24),new Vector2(128,128));
            casterStatus=Label(selectedCard,"",new Vector2(492,-102),new Vector2(700,50),18,new Color(.8f,.65f,1),TextAnchor.UpperLeft);
            actionCard=Section(matchHud,"Selected item actions",Vector2.zero,new Vector2(HudInnerWidth,468));
            optionsTitle=HudTitle(actionCard,"OPTIONS");
            actionHint=Label(actionCard,"",new Vector2(40,-48),new Vector2(HudInnerWidth-80,100),24,ConsoleTheme.Muted,TextAnchor.MiddleCenter);
            harvestingStrip=Section(matchHud,"Harvesting information",Vector2.zero,new Vector2(HudInnerWidth,28),Color.clear);
            groupStrip=Section(matchHud,"Control groups",Vector2.zero,new Vector2(HudInnerWidth,184));
            HudTitle(groupStrip,"CONTROL GROUPS");
            HudSectionHelp(groupStrip,"Control groups help","CONTROL GROUPS",()=>Quest.Active?"Trigger: select · Grip + Trigger: assign":"Click: select · Ctrl + click: assign");
            for(int g=0;g<GameSession.ControlGroupCount;g++)
            {
                int index=g;
                var target=groupTargets[g]=HudButton(groupStrip,"Group "+(g+1),"Empty",new Vector2(HudPadding+g*346,-HudContentTop),new Vector2(330,84),()=>Session.Group(index,false),null,out groupCounts[g]);
                var number=target.rect.GetComponentInChildren<Text>();number.text=(g+1).ToString();number.fontSize=34;number.fontStyle=FontStyle.Bold;
                TopLeft(number.rectTransform,new Vector2(16,-8),new Vector2(48,68));
                TopLeft(groupCounts[g].rectTransform,new Vector2(84,-16),new Vector2(230,52));groupCounts[g].fontSize=20;groupCounts[g].alignment=TextAnchor.MiddleLeft;
                CreateGroupFeedback(g,target);
            }
            matchStrip=Section(matchHud,"Match controls",Vector2.zero,new Vector2(HudInnerWidth,132));
            matchMap=Label(matchStrip,"",new Vector2(HudPadding,-HudPadding),new Vector2(824,36),24,ConsoleTheme.Ink);matchMap.fontStyle=FontStyle.Bold;
            matchInfo=Label(matchStrip,"",new Vector2(HudPadding,-68),new Vector2(824,40),20,ConsoleTheme.Muted);
            actionHeading=Label(matchStrip,"ACTIONS",new Vector2(HudPadding,-68),new Vector2(824,40),20,ConsoleTheme.Ink);actionHeading.fontStyle=FontStyle.Bold;
            matchClock=Label(matchStrip,"00:00",new Vector2(872,-HudPadding),new Vector2(224,84),32,ConsoleTheme.Ink,TextAnchor.MiddleRight);matchClock.fontStyle=FontStyle.Bold;
            matchTargets[0]=HudButton(matchStrip,"Full map","",Vector2.zero,new Vector2(264,84),()=>Board.Fit(),null,out _);
            matchTargets[0].rect.gameObject.SetActive(false);
            var pause=matchTargets[1]=HudButton(matchStrip,"Pause","",new Vector2(1120,-HudPadding),new Vector2(272,84),()=>Show(MenuPage.Pause),null,out var pauseDetail);pauseDetail.gameObject.SetActive(false);
            var pauseName=pause.rect.GetComponentInChildren<Text>();pauseName.fontSize=24;pauseName.fontStyle=FontStyle.Bold;
            TopLeft(pauseName.rectTransform,new Vector2(76,0),new Vector2(180,84));
            Glyph(pause.rect,ConsoleGlyph.Kind.Pause,new Vector2(24,-24),36,ConsoleTheme.Ink);
            CreateProductionHud();CreateSelectionGrid();
            HudSectionHelp(selectedCard,"Selection help","SELECTION",()=>((LocalizedText)selectionGridHint).Source+
                (string.IsNullOrEmpty(casterStatus.text)?"":"\n\n"+((LocalizedText)casterStatus).Source)+
                (SelectedMobileUnits().Any(e=>Session.Catalog[e.definition].role=="worker")?"\n\nOptimal: 2 workers per mineral patch · 3 per gas · Automatically assigned to nearby deposits":""));
            HudSectionHelp(productionStrip,"Production help","PRODUCTION",()=>((LocalizedText)productionHint).Source);
            HudSectionHelp(actionCard,"Actions help","OPTIONS",()=>"Hover an action to learn more");
            orderPlanStrip=Section(matchHud,"Unit order plan",Vector2.zero,new Vector2(HudInnerWidth,236));
            orderPlanTitle=HudTitle(orderPlanStrip,"QUEUED ORDERS");
            orderPlanCount=Label(orderPlanStrip,"",new Vector2(350,-HudPadding),new Vector2(970,28),20,ConsoleTheme.Ink,TextAnchor.MiddleRight);
            CreateOrderCards();
            orderPlanHint=Label(orderPlanStrip,"",new Vector2(16,-66),new Vector2(1020,23),14,ConsoleTheme.Muted);orderPlanHint.gameObject.SetActive(false);
            HudSectionHelp(orderPlanStrip,"Orders help","QUEUED ORDERS",()=>((LocalizedText)orderPlanHint).Source);
            rallyPointStrip=Section(matchHud,"Rally point",Vector2.zero,new Vector2(HudInnerWidth,52));FlatHud(rallyPointStrip);
            Label(rallyPointStrip,"RALLY POINT",new Vector2(16,-7),new Vector2(230,24),16,MineralTint);
            rallyPointValue=Label(rallyPointStrip,"",new Vector2(260,-10),new Vector2(1090,30),20,ConsoleTheme.Ink,TextAnchor.MiddleRight);
            rallyPointHint=Label(rallyPointStrip,"",new Vector2(16,-38),new Vector2(1020,24),16,ConsoleTheme.Muted);rallyPointHint.gameObject.SetActive(false);
            rallyPointStrip.gameObject.SetActive(false);
        }
        private Text HudTitle(RectTransform parent,string title)
        {
            var label=Label(parent,title,new Vector2(HudPadding,-HudPadding),new Vector2(900,28),(int)HudTitleSize,ConsoleTheme.Muted);
            label.fontStyle=FontStyle.Bold;return label;
        }
        private void HudSectionHelp(RectTransform parent,string name,string title,Func<string> hint)
            =>HudHelpButton(parent,name,new Vector2(HudInnerWidth-HudPadding-44,-16),title,hint);
        private static void FlatHud(RectTransform rect)
        {
            var fill=rect.GetComponent<Image>();fill.sprite=null;fill.type=Image.Type.Simple;
        }
        private RectTransform Section(RectTransform parent,string name,Vector2 position,Vector2 size,Color? color=null)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);TopLeft(rect,position,size);
            var image=go.GetComponent<Image>();image.color=color??ConsoleTheme.Surface;image.raycastTarget=false;
            ConsoleTheme.StyleSurface(image);return rect;
        }
        private Text ResourceCard(string label,float x,Color tint,ConsoleGlyph.Kind icon,out Text hint)
        {
            var card=Section(resourceStrip,label,new Vector2(x,0),new Vector2(352,100),Color.clear);
            Glyph(card,icon,new Vector2(0,-18),64,tint);
            Label(card,label,new Vector2(84,-6),new Vector2(264,26),20,tint);
            var value=Label(card,"0",new Vector2(84,-32),new Vector2(248,64),48,ConsoleTheme.Ink);value.fontStyle=FontStyle.Bold;
            hint=Label(card,"",new Vector2(340,-38),new Vector2(124,44),16,tint,TextAnchor.MiddleRight);
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
            var name=Label(rect,title,new Vector2(8,compact?-2:-3),new Vector2(size.x-16,compact?21:25),compact?16:18,ConsoleTheme.Ink,TextAnchor.MiddleCenter);
            if(compact){name.resizeTextForBestFit=true;name.resizeTextMinSize=13;name.resizeTextMaxSize=16;}
            detail=Label(rect,subtitle,new Vector2(8,compact?-23:-28),new Vector2(size.x-16,compact?18:21),compact?13:14,ConsoleTheme.Muted,TextAnchor.MiddleCenter);
            if(model!=null)
            {
                float portraitSize=Mathf.Min(44,size.y-10),left=portraitSize+16;
                ModelPortrait(rect,new Vector2(8,-5),new Vector2(portraitSize,portraitSize),model);
                name.text=model.name;name.alignment=detail.alignment=TextAnchor.MiddleLeft;
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
            LayoutSelectionGrid(hud);
            orderPlanStrip.gameObject.SetActive(orders);
            productionStrip.gameObject.SetActive(production);
            rallyPointStrip.gameObject.SetActive(false);
            matchHud.gameObject.SetActive(hud);
            matchStrip.gameObject.SetActive(hud);
            matchTargets[0].rect.gameObject.SetActive(false);
            matchTargets[1].rect.GetComponentInChildren<Text>().text=Session.IsOnline?"Menu":"Pause";
            headline.gameObject.SetActive(!hud);economy.gameObject.SetActive(!hud);
            selection.fontSize=20;selection.fontStyle=hud?FontStyle.Bold:FontStyle.Normal;selection.color=hud?ConsoleTheme.Ink:ConsoleTheme.Muted;
            selection.alignment=TextAnchor.MiddleLeft;
            TopLeft(selection.rectTransform,hud?new Vector2(1280,-HudBodyTop-HudPadding):new Vector2(30,-169),hud?new Vector2(72,28):new Vector2(1040,38));
            harvesting.gameObject.SetActive(true);harvesting.fontSize=hud?18:18;
            status.fontSize=replay?16:hud?18:20;
            if(hud)
            {
                float selectedHeight=HudSelectionHeight;
                TopLeft(selectedCard,new Vector2(32,-HudBodyTop),new Vector2(HudInnerWidth,selectedHeight));
                float next=HudBodyTop+selectedHeight+HudGap;
                float actionHeight=MatchActionsHeight();
                TopLeft(actionCard,new Vector2(32,-next),new Vector2(HudInnerWidth,HudContentTop+actionHeight+HudPadding));
                TopLeft(actions,new Vector2(32+HudPadding,-next-HudContentTop),new Vector2(HudInnerWidth-HudPadding*2,actionHeight));
                next+=actionCard.rect.height+HudGap;
                // Production and unit orders use the same reserved region. Empty,
                // resource and mixed selections never move the controls below it.
                productionStrip.anchoredPosition=orderPlanStrip.anchoredPosition=new Vector2(32,-next);
                next+=productionStrip.rect.height+HudGap;
                groupStrip.anchoredPosition=new Vector2(32,-next);next+=groupStrip.rect.height+HudGap;
                TopLeft(harvestingStrip,new Vector2(32,-next),new Vector2(HudInnerWidth,28));
                TopLeft(harvesting.rectTransform,new Vector2(32+HudPadding,-next-2),new Vector2(HudInnerWidth-HudPadding*2,24));next+=32;
                // Footer text follows the section content inset, without the resource icon offset.
                TopLeft(statusBanner,new Vector2(32+HudPadding,-next),new Vector2(HudInnerWidth-HudPadding*2,48));
                next+=48+HudGap;
                matchStrip.anchoredPosition=new Vector2(32,-next);
                root.sizeDelta=new Vector2(HudWidth,next+matchStrip.rect.height+32);
            }
            else
            {
                root.sizeDelta=new Vector2(1100,guide?920:controls?1004:reference||history?966:report?1028:replay?1082:690);
                TopLeft(harvesting.rectTransform,new Vector2(30,-207),new Vector2(1040,28));
                TopLeft(statusBanner,new Vector2(30,guide?-860:controls?-938:reference||history?-910:report?-972:replay?-1020:-638),new Vector2(1040,48));
                TopLeft(actions,new Vector2(30,guide?-786:controls?-866:reference?-840:-240),new Vector2(1040,report?716:history?648:replay?756:controls||guide||reference?58:396));
            }
            matchHud.sizeDelta=root.sizeDelta;
            LayoutConsoleChrome();
        }
        private void UpdateMatchHud()
        {
            var view=Session.View;
            mineralValue.text=view.minerals.ToString("N0",CultureInfo.InvariantCulture);gasValue.text=view.catalyst.ToString("N0",CultureInfo.InvariantCulture);
            supplyValue.text=$"{view.supply} / {view.capacity}";
            bool full=view.supply>=view.capacity;
            supplyValue.color=full?WarningTint:ConsoleTheme.Ink;supplyHint.text="";
            matchClock.text=$"{view.tick/1200:00}:{view.tick/20%60:00}";
            matchMap.text=Session.Map.title;matchInfo.text=MatchDescription;
            for(int g=0;g<GameSession.ControlGroupCount;g++){int count=Session.GroupSize(g);groupCounts[g].text=count==0?"Empty":count+" assigned";}

            var selected=view.entities.Where(e=>Session.Selection.Contains(e.id)).ToArray();
            selectedKind.text="SELECTION";selectionRole.text="";selectionHealth.text="";selectionDetails.text="";casterStatus.text="";
            healthBackground.gameObject.SetActive(false);selectedPortrait.gameObject.SetActive(false);
            TopLeft(selectionDetails.rectTransform,new Vector2(492,-32),new Vector2(800,58));
            if(selected.Length==1)DescribeEntity(selected[0],false);
            else if(selected.Length>1)
            {
                selectedKind.text="MULTIPLE SELECTION";
                selection.text=selected.Length+(selected.All(e=>!Session.Catalog[e.definition].building)?" units":selected.All(e=>Session.Catalog[e.definition].building)?" buildings":" items");
                selectionRole.text="SELECTED COMPOSITION";
                var types=selected.GroupBy(e=>e.definition).OrderByDescending(g=>g.Count()).ToArray();
                selectionDetails.text=string.Join("\n",types.Take(5).Select(g=>$"{g.Count()} × {Session.Catalog[g.Key].name}"));
                if(types.Length>5)((LocalizedText)selectionDetails).Append($"\n+ {types.Skip(5).Sum(g=>g.Count())} other items");
                TopLeft(selectionDetails.rectTransform,new Vector2(492,-32),new Vector2(800,100));
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
            // Selected items occupy the whole row. Hover-only information uses
            // the same content area when there are no selection cards.
            bool fallback=selected.Length==0&&(view.resources.Any(r=>r.id==(Session.SelectedResource>0?Session.SelectedResource:Board.HoveredResource))
                ||view.entities.Any(e=>e.id==Board.Hovered&&!e.remembered));
            selectionFallbackTitle.text=selection.text;
            selectionFallbackTitle.gameObject.SetActive(fallback);
            selectionRole.gameObject.SetActive(fallback);selectionDetails.gameObject.SetActive(fallback);
            TopLeft(selectionRole.rectTransform,new Vector2(112,-HudContentTop-40),new Vector2(560,28));
            TopLeft(selectionDetails.rectTransform,new Vector2(704,-HudContentTop),new Vector2(664,100));
            TopLeft(selectedPortrait.rectTransform,new Vector2(HudPadding,-HudContentTop),new Vector2(64,64));
            selectedPortrait.gameObject.SetActive(fallback&&selectedPortrait.gameObject.activeSelf);
            TopLeft(healthBackground.rectTransform,new Vector2(112,-HudContentTop-116),new Vector2(264,6));
            TopLeft(selectionHealth.rectTransform,new Vector2(112,-HudContentTop-80),new Vector2(560,28));
            healthBackground.gameObject.SetActive(fallback&&healthBackground.gameObject.activeSelf);
            selectionHealth.gameObject.SetActive(fallback&&!string.IsNullOrEmpty(selectionHealth.text));
            selectedKind.text="SELECTION";selection.text=selected.Length>0?selected.Length.ToString():"";
            UpdateCasterStatus(selected);
            casterStatus.gameObject.SetActive(false);
            UpdateMatchActionDetails();
            UpdateHarvesting(selected);
            bool contextualHarvesting=!string.IsNullOrEmpty(harvesting.text)&&harvesting.text!=Localization.Text("Optimal: 2 workers per mineral patch · 3 per gas · Automatically assigned to nearby deposits");
            harvesting.gameObject.SetActive(contextualHarvesting);harvestingStrip.gameObject.SetActive(contextualHarvesting);
            UpdateProductionHud();
            UpdateSelectionGrid();
            UpdateOrderPlanHud();
            UpdateRallyPointHud();
            LayoutHudCounters();
            UpdateGroupFeedback();
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
            TopLeft(casterStatus.rectTransform,casters.Length>1?new Vector2(20,-100):new Vector2(492,-102),casters.Length>1?new Vector2(264,68):new Vector2(700,50));
            casterStatus.text=$"ENERGY {Mathf.FloorToInt(energy)} / {Mathf.CeilToInt(capacity)}"+(casters.Length>1?$" · {casters.Length} casters":"")+$"\n{ready} / {total} abilities ready";
        }
        private void UpdateMatchActionDetails()
        {
            // Cooldowns change every tick. Refresh labels without rebuilding buttons,
            // which would invalidate a press that is still held on the console.
            var available=SelectionActions.For(Session);
            actionHeading.text=Session.BuildDefinition!=null?"PLACING · "+Session.Catalog[Session.BuildDefinition].name
                :Session.AbilityDefinitionId!=null?"TARGET · "+Session.PendingAbility.name:available.Heading;
            actionHeading.gameObject.SetActive(Session.BuildDefinition!=null||Session.AbilityDefinitionId!=null);
            matchInfo.gameObject.SetActive(!actionHeading.gameObject.activeSelf);
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
        private static string PlanOrderLabel(CommandKind order)
        {
            switch(order){case CommandKind.Move:return "Move";case CommandKind.Attack:return "Attack";case CommandKind.AttackMove:return "Attack Move";case CommandKind.Gather:return "Gather";case CommandKind.Build:return "Build";case CommandKind.Repair:return "Repair";case CommandKind.Hold:return "Hold Position";case CommandKind.Cast:return "Cast";default:return "Stop";}
        }
        private void DescribeEntity(VisibleEntity entity,bool pointing)
        {
            var type=Session.Catalog[entity.definition];
            selectedKind.text=pointing?"POINTING AT · "+(entity.owner==Session.View.player?"ALLY":"ENEMY"):type.building?"SELECTED BUILDING":"SELECTED UNIT";
            selection.text=type.name;
            selectedPortrait.SetType(type);selectedPortrait.gameObject.SetActive(true);
            TopLeft(selectionDetails.rectTransform,new Vector2(492,-32),new Vector2(800,72));
            selectionRole.text=(type.building?BuildingRole(type.role):UnitVisuals.RoleName(type.role))+" · "+ShortFaction(type.faction);
            float ratio=Mathf.Clamp01(entity.health/type.health);healthBackground.gameObject.SetActive(true);
            healthFill.rectTransform.sizeDelta=new Vector2(264*ratio,6);healthFill.color=ratio<.3f?WarningTint:SupplyTint;
            selectionHealth.text=$"{Mathf.CeilToInt(entity.health)} / {Mathf.CeilToInt(type.health)} HP";
            if(!entity.complete)selectionDetails.text=$"UNDER CONSTRUCTION · {Mathf.Min(99,Mathf.FloorToInt(entity.progress*100))}%\n"+
                (type.role=="hq"?"Cannot receive resources yet": "Not operational yet");
            else if(entity.queue.Length>0)
            {
                selectionDetails.text=pointing?$"{entity.queue.Length} queued\nSelect to view production":$"1 active · {entity.queue.Length-1} waiting";
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
        // The empty state and every selection share a fixed three-row grid.
        private float MatchActionsHeight()=>HudActionHeight+HudActionStride*2;
        private void BuildMatchActions()
        {
            targets.AddRange(groupTargets);targets.AddRange(matchTargets.Where(t=>t.rect.gameObject.activeSelf));
            BindProductionTargets();BindOrderTargets();BindSelectionTargets();BindHudHelpTargets();
            var available=SelectionActions.For(Session);actionHeading.text=available.Heading;
            actionHint.text="";actionHint.gameObject.SetActive(false);
            // Non-interactive slots remain visible, even when the selection has no commands.
            var slots=new GameObject("Option placeholders",typeof(RectTransform)).GetComponent<RectTransform>();
            slots.SetParent(actions,false);TopLeft(slots,Vector2.zero,actions.sizeDelta);
            for(int row=0;row<3;row++)for(int column=0;column<4;column++)
                Section(slots,"Empty option",new Vector2(column==3?1040:column*(HudActionWidth+HudGap),-row*HudActionStride),
                    new Vector2(HudActionWidth,HudActionHeight),Color.Lerp(ConsoleTheme.Surface,button,.32f));
            foreach(var action in available.Items.Where(a=>a.Category!=ActionCategory.Cancel))
            {
                var captured=action;
                bool side=action.Category==ActionCategory.Orders||action.Category==ActionCategory.Research;
                int slot=MatchActionSlot(action,available);
                float x=side?1040:slot%3*(HudActionWidth+HudGap),y=(side?slot:slot/3)*HudActionStride;
                bool active=captured.Building&&captured.Id=="build:"+Session.BuildDefinition;
                var tint=ActionTint(action.Category);
                var target=HudButton(actions,captured.Title,MatchActionDetail(captured,active),new Vector2(x,-y),new Vector2(HudActionWidth,HudActionHeight),()=>captured.Execute(),null,out var detail,captured.Model);
                target.normal=active?ConsoleTheme.Primary:Color.Lerp(button,tint.linear,.12f);
                target.image.color=target.normal;target.help=captured;target.actionId=captured.Id;
                var name=target.rect.GetComponentInChildren<Text>();
                name.fontSize=22;name.fontStyle=FontStyle.Bold;name.resizeTextForBestFit=false;
                name.alignment=detail.alignment=TextAnchor.MiddleLeft;
                detail.color=Color.Lerp(ConsoleTheme.Ink,tint,.35f);detail.fontSize=20;
                if(captured.Model!=null)
                {
                    var portrait=target.rect.GetComponentInChildren<SelectionPortrait>();
                    TopLeft(portrait.rectTransform,new Vector2(16,-32),new Vector2(48,48));
                    TopLeft(name.rectTransform,new Vector2(80,-16),new Vector2(HudActionWidth-96,48));
                    TopLeft(detail.rectTransform,new Vector2(80,-68),new Vector2(HudActionWidth-96,28));
                }
                else
                {
                    bool order=captured.Category==ActionCategory.Orders;
                    detail.gameObject.SetActive(!order);
                    TopLeft(name.rectTransform,new Vector2(24,order?0:-12),new Vector2(HudActionWidth-48,order?HudActionHeight:48));
                    TopLeft(detail.rectTransform,new Vector2(24,-64),new Vector2(HudActionWidth-48,32));
                }
                if(active){detail.color=ConsoleTheme.Ink;detail.fontStyle=FontStyle.Bold;}
                targets.Add(target);actionIndex++;
            }
        }
        private int MatchActionSlot(SelectionAction action,SelectionActions available)
        {
            // Use the faction catalog, never the currently available subset:
            // selecting another producer cannot move a known action.
            if(action.Category==ActionCategory.Orders)
                return action.Id=="attack-move"?0:action.Id=="stop"?1:2;
            if(action.Category==ActionCategory.Research)
                return Array.FindIndex(Session.Catalog.Definition.upgrades,u=>action.Id=="research:"+u.id);
            if(action.Category==ActionCategory.Abilities)
                return Array.FindIndex(Session.Catalog.Definition.archetypes.Where(t=>t.faction==Session.Faction)
                    .SelectMany(t=>t.abilities).Distinct().ToArray(),id=>action.Id=="ability:"+id);
            if(action.Category==ActionCategory.Train)
            {
                // A single producer uses its own compact roster. A multi-role
                // selection uses the combined roster in the same catalog order.
                var train=available.Items.Where(a=>a.Category==ActionCategory.Train).ToArray();
                return Array.FindIndex(train,a=>a.Id==action.Id);
            }
            var roles=action.Category==ActionCategory.Build
                ?new[]{"hq","extractor","supply","barracks","tech","academy","defense"}
                :new[]{"worker","scout","melee","ranged","siege","caster"};
            return Array.IndexOf(roles,action.Model.role);
        }
        private void LayoutHudCounter(Text title,Text count,RectTransform section)
        {
            float left=HudPadding+title.preferredWidth+12;
            Vector2 position=new Vector2(left,-HudPadding);
            if(count.transform.parent!=section)position+=(Vector2)section.anchoredPosition;
            TopLeft(count.rectTransform,position,new Vector2(HudInnerWidth-left-HudPadding-60,28));
            count.alignment=TextAnchor.MiddleLeft;
        }
        private void LayoutHudCounters()
        {
            if(Page!=MenuPage.Actions||root==null||!root.gameObject.activeInHierarchy)return;
            LayoutHudCounter(selectedKind,selection,selectedCard);
            LayoutHudCounter(productionTitle,productionCount,productionStrip);
            LayoutHudCounter(orderPlanTitle,orderPlanCount,orderPlanStrip);
        }
        private static string MatchActionDetail(SelectionAction action,bool active)
        {
            // Keep costs readable; roles and requirements live in hover help.
            var model=action.Model;
            string detail=model==null?action.Detail:model.minerals+" M"+(model.catalyst>0?" / "+model.catalyst+" G":"");
            if(action.Category==ActionCategory.Research)detail=detail.Substring(detail.IndexOf('·')+1).Trim();
            return detail;
        }
    }
}
