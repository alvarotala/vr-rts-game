#if FRONTIER_BROWSER
using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // Browser presentation only. Commands, costs, production and selection come
    // from the same GameSession/PlayerView/SelectionActions used by the VR client.
    public sealed partial class CommandPanel
    {
        private const float BrowserBarHeight=60,BrowserDockHeight=244,BrowserCommandWidth=470;
        private RectTransform browserHud,browserBar,browserInfo,browserCommandCard,browserMapCard,browserGroupBar,browserQueue;
        private Text browserMinerals,browserGas,browserSupply,browserClock,browserName,browserDescription,browserHealth,browserQueueTitle,browserCommandTitle;
        private Text browserIdle,browserArmy;
        private SelectionPortrait browserPortrait;
        private BrowserMinimap browserMinimap;
        private readonly List<Target> browserButtons=new List<Target>(),browserQueueTargets=new List<Target>();
        private readonly List<(int producer,int order,int depth,Text state,Image fill,float width)> browserProductionItems=new List<(int,int,int,Text,Image,float)>();
        private int browserActionPage;
        private string browserActionContext,browserQueueSignature;
        private Vector2 browserScreenSize;
        private uint browserMessageVersion;
        private float browserMessageUntil;
        private readonly RectTransform[] browserTutorialEdges=new RectTransform[4];

        private void CreateBrowserHud()
        {
            browserHud=new GameObject("Desktop HUD",typeof(RectTransform)).GetComponent<RectTransform>();browserHud.SetParent(root,false);
            browserBar=Section(browserHud,"Top bar",Vector2.zero,new Vector2(1600,60),ConsoleTheme.Background);
            var menu=BrowserButton(browserBar,"Menu",new Vector2(12,-10),new Vector2(104,40),ToggleMenu);
            matchTargets[1]=menu;browserButtons.Add(menu);
            browserMinerals=BrowserResource(browserBar,"Minerals",130,MineralTint,ConsoleGlyph.Kind.Mineral);
            browserGas=BrowserResource(browserBar,"Gas",294,GasTint,ConsoleGlyph.Kind.Gas);
            browserSupply=BrowserResource(browserBar,"Population",444,SupplyTint,ConsoleGlyph.Kind.Supply);
            var idle=BrowserButton(browserBar,"Idle workers",new Vector2(624,-10),new Vector2(150,40),()=>Session.SelectIdleWorkers());browserIdle=idle.rect.GetComponentInChildren<Text>();browserButtons.Add(idle);
            var army=BrowserButton(browserBar,"Army",new Vector2(784,-10),new Vector2(130,40),()=>Session.SelectArmy());browserArmy=army.rect.GetComponentInChildren<Text>();browserButtons.Add(army);
            browserClock=Label(browserBar,"00:00",new Vector2(1450,-10),new Vector2(124,40),22,ConsoleTheme.Ink,TextAnchor.MiddleRight);
            browserMapCard=Section(browserHud,"Minimap frame",Vector2.zero,new Vector2(244,244),ConsoleTheme.Background);
            browserMinimap=new GameObject("Minimap",typeof(RectTransform),typeof(BrowserMinimap)).GetComponent<BrowserMinimap>();
            browserMinimap.transform.SetParent(browserMapCard,false);TopLeft(browserMinimap.rectTransform,new Vector2(10,-10),new Vector2(224,224));
            browserMinimap.Session=Session;browserMinimap.Board=Board;browserMinimap.Quest=Quest;
            browserInfo=Section(browserHud,"Selection and production",Vector2.zero,new Vector2(800,244),ConsoleTheme.Background);
            browserPortrait=ModelPortrait(browserInfo,new Vector2(16,-18),new Vector2(82,82));
            browserName=Label(browserInfo,"No selection",new Vector2(114,-18),new Vector2(600,32),24,ConsoleTheme.Ink);browserName.fontStyle=FontStyle.Bold;
            browserHealth=Label(browserInfo,"",new Vector2(114,-54),new Vector2(600,26),17,ConsoleTheme.Accent);
            browserDescription=Label(browserInfo,"",new Vector2(114,-84),new Vector2(600,52),16,ConsoleTheme.Muted,TextAnchor.UpperLeft);
            browserQueueTitle=Label(browserInfo,"",new Vector2(16,-140),new Vector2(600,24),14,ConsoleTheme.Muted);
            browserQueue=new GameObject("Production and orders",typeof(RectTransform)).GetComponent<RectTransform>();browserQueue.SetParent(browserInfo,false);
            TopLeft(browserQueue,new Vector2(16,-170),new Vector2(700,58));
            browserCommandCard=Section(browserHud,"Commands",Vector2.zero,new Vector2(BrowserCommandWidth,BrowserDockHeight),ConsoleTheme.Background);
            browserCommandTitle=Label(browserCommandCard,"OPTIONS",new Vector2(12,-8),new Vector2(446,26),16,ConsoleTheme.Muted);
            browserGroupBar=Section(browserHud,"Control groups",Vector2.zero,new Vector2(584,40),Color.clear);
            for(int i=0;i<GameSession.ControlGroupCount;i++)
            {
                int group=i;
                var target=BrowserButton(browserGroupBar,(i+1).ToString(),new Vector2(i*146,0),new Vector2(138,38),()=>Session.Group(group,false));
                TopLeft(target.rect.GetComponentInChildren<Text>().rectTransform,new Vector2(6,0),new Vector2(24,38));
                groupTargets[i]=target;groupCounts[i]=Label(target.rect,"Empty",new Vector2(36,0),new Vector2(96,38),14,ConsoleTheme.Muted);CreateGroupFeedback(i,target);
            }
        }

        private Target BrowserButton(RectTransform parent,string title,Vector2 position,Vector2 size,Action click)
        {
            var target=HudButton(parent,title,"",position,size,click,null,out var detail);detail.gameObject.SetActive(false);
            var name=target.rect.GetComponentInChildren<Text>();TopLeft(name.rectTransform,new Vector2(8,0),size-new Vector2(16,0));
            name.fontSize=17;name.resizeTextForBestFit=false;name.alignment=TextAnchor.MiddleCenter;
            return target;
        }

        private Text BrowserResource(RectTransform parent,string title,float x,Color tint,ConsoleGlyph.Kind glyph)
        {
            Glyph(parent,glyph,new Vector2(x,-19),24,tint);
            var value=Label(parent,"0",new Vector2(x+34,-10),new Vector2(120,40),23,ConsoleTheme.Ink);value.fontStyle=FontStyle.Bold;
            value.gameObject.name=title;return value;
        }

        private void LayoutBrowserPage()
        {
            if(browserHud==null)return;
            bool hud=Page==MenuPage.Actions;
            browserHud.gameObject.SetActive(hud);matchHud.gameObject.SetActive(false);
            moveHandle.gameObject.SetActive(false);resetHandle.gameObject.SetActive(false);
            if(hud)
            {
                foreach(var text in new[]{headline,economy,selection,harvesting})text.gameObject.SetActive(false);
                consoleBrand.gameObject.SetActive(false);consoleBrandSmall.gameObject.SetActive(false);
                root.GetComponent<Image>().color=Color.clear;
            }
            else root.GetComponent<Image>().color=background;
            actions.SetAsLastSibling();statusBanner.SetAsLastSibling();
            LayoutBrowserScreen();
        }

        private void LayoutBrowserScreen()
        {
            if(browserHud==null)return;
            Vector2 size=((RectTransform)canvas.transform).rect.size;
            if(size.x<1||size.y<1)return;
            bool hud=Page==MenuPage.Actions;
            if(Page==MenuPage.Replay){LayoutBrowserReplay(size);return;}
            root.anchorMin=root.anchorMax=root.pivot=Vector2.one*.5f;
            root.anchoredPosition=Vector2.zero;root.localRotation=Quaternion.identity;
            if(!hud)
            {
                float fit=Mathf.Min(1,Mathf.Min((size.x-64)/root.sizeDelta.x,(size.y-64)/root.sizeDelta.y));
                root.localScale=Vector3.one*fit;
                Quest.Navigation?.SetViewport(new Rect(0,0,1,1));
                return;
            }
            root.localScale=Vector3.one;root.sizeDelta=size;
            root.GetComponent<Image>().color=Color.clear;
            TopLeft(browserHud,Vector2.zero,size);
            TopLeft(browserBar,new Vector2(12,-12),new Vector2(size.x-24,BrowserBarHeight));
            TopLeft(browserClock.rectTransform,new Vector2(size.x-164,-10),new Vector2(124,40));
            float dockTop=size.y-BrowserDockHeight-12,infoWidth=size.x-24-244-BrowserCommandWidth-24;
            TopLeft(browserMapCard,new Vector2(12,-dockTop),new Vector2(244,BrowserDockHeight));
            TopLeft(browserInfo,new Vector2(268,-dockTop),new Vector2(infoWidth,BrowserDockHeight));
            TopLeft(browserCommandCard,new Vector2(size.x-BrowserCommandWidth-12,-dockTop),new Vector2(BrowserCommandWidth,BrowserDockHeight));
            TopLeft(browserGroupBar,new Vector2(268,-dockTop+46),new Vector2(584,40));
            TopLeft(actions,new Vector2(size.x-BrowserCommandWidth,-dockTop-38),new Vector2(BrowserCommandWidth-24,194));
            TopLeft(browserName.rectTransform,new Vector2(114,-18),new Vector2(infoWidth-130,32));
            TopLeft(browserHealth.rectTransform,new Vector2(114,-54),new Vector2(infoWidth-130,26));
            TopLeft(browserDescription.rectTransform,new Vector2(114,-84),new Vector2(infoWidth-130,52));
            browserQueue.sizeDelta=new Vector2(infoWidth-32,58);
            TopLeft(browserQueueTitle.rectTransform,new Vector2(16,-140),new Vector2(infoWidth-32,24));
            TopLeft(statusBanner,new Vector2(280,-82),new Vector2(Mathf.Max(300,size.x-560),40));
            status.fontSize=17;status.alignment=TextAnchor.MiddleCenter;
            TopLeft(status.rectTransform,Vector2.zero,statusBanner.sizeDelta);
            Quest.Navigation?.SetViewport(new Rect(0,(BrowserDockHeight+70)/size.y,1,Mathf.Max(.2f,(size.y-BrowserDockHeight-150)/size.y)));
            if(browserScreenSize!=size){browserScreenSize=size;browserQueueSignature=null;}
        }

        private bool BrowserContainsPointer(Ray ray)
        {
            if(Hit(tutorialCard,ray,out _))return true;
            if(Page==MenuPage.Replay)return BrowserReplayContainsPointer(ray);
            if(Page!=MenuPage.Actions)return Hit(root,ray,out _);
            if(UnityEngine.InputSystem.Mouse.current!=null&&!Quest.Camera.pixelRect.Contains(UnityEngine.InputSystem.Mouse.current.position.ReadValue()))return true;
            return Hit(browserBar,ray,out _)||Hit(browserMapCard,ray,out _)||Hit(browserInfo,ray,out _)
                ||Hit(browserCommandCard,ray,out _)||Hit(browserGroupBar,ray,out _)||Hit(actionHelp,ray,out _);
        }

        private void BuildBrowserActions()
        {
            targets.AddRange(browserButtons);targets.AddRange(groupTargets);
            var available=SelectionActions.For(Session);
            string context=string.Join(",",Session.Selection.OrderBy(id=>id))+":"+string.Join(",",available.Items.Select(a=>a.Id));
            if(context!=browserActionContext){browserActionContext=context;browserActionPage=0;}
            const int pageSize=15;
            int pages=Mathf.Max(1,(available.Items.Count+pageSize-1)/pageSize);
            browserActionPage=Mathf.Clamp(browserActionPage,0,pages-1);
            browserCommandTitle.text=available.Heading;
            float width=(BrowserCommandWidth-24-16)/5;
            for(int slot=0;slot<pageSize;slot++)
            {
                int index=browserActionPage*pageSize+slot;
                var position=new Vector2(slot%5*(width+4),-slot/5*60);
                if(index>=available.Items.Count){Section(actions,"Empty option",position,new Vector2(width,56),ConsoleTheme.Surface);continue;}
                var action=available.Items[index];
                var target=HudButton(actions,action.Label,MatchActionDetail(action,false),position,new Vector2(width,56),()=>action.Execute(),null,out var detail);
                target.actionId=action.Id;target.help=action;
                bool active=action.Building&&action.Id=="build:"+Session.BuildDefinition||action.Id=="attack-move"&&Session.AttackMoveMode||action.Id=="set-rally-point"&&Session.RallyPointMode;
                target.normal=active?ConsoleTheme.Primary:Color.Lerp(button,ActionTint(action.Category).linear,.16f);target.image.color=target.normal;
                var title=target.rect.GetComponentInChildren<Text>();title.fontSize=13;title.fontStyle=FontStyle.Bold;
                TopLeft(title.rectTransform,new Vector2(4,-3),new Vector2(width-8,30));
                title.resizeTextForBestFit=true;title.resizeTextMinSize=11;title.resizeTextMaxSize=13;
                TopLeft(detail.rectTransform,new Vector2(4,-34),new Vector2(width-8,19));detail.fontSize=11;
                detail.color=ConsoleTheme.Muted;targets.Add(target);
            }
            if(pages>1)
            {
                var next=BrowserButton(actions,(browserActionPage+1)+" / "+pages+"  ›",new Vector2(320,-180),new Vector2(126,22),()=>{browserActionPage=(browserActionPage+1)%pages;Rebuild();});
                next.rect.GetComponentInChildren<Text>().fontSize=13;targets.Add(next);
            }
            browserQueueSignature=null;RefreshBrowserQueues();UpdateBrowserHud();
        }

        private void UpdateBrowserHud()
        {
            var view=Session.View;if(view==null||browserHud==null)return;
            browserMinerals.text=view.minerals.ToString("N0");browserGas.text=view.catalyst.ToString("N0");browserSupply.text=view.supply+" / "+view.capacity;
            browserSupply.color=view.supply>=view.capacity?WarningTint:ConsoleTheme.Ink;
            browserClock.text=ReportTime(view.tick);
            ((LocalizedText)browserIdle).SetKey("Idle workers: {0}",Session.IdleWorkers.Count());
            ((LocalizedText)browserArmy).SetKey("Army: {0}",Session.Army.Count());
            var selected=view.entities.Where(e=>Session.Selection.Contains(e.id)).ToArray();
            browserPortrait.gameObject.SetActive(selected.Length>0);browserHealth.text="";
            if(selected.Length==1)
            {
                var entity=selected[0];var type=Session.Catalog[entity.definition];browserName.text=type.name;browserPortrait.SetType(type);
                browserHealth.text=Mathf.CeilToInt(entity.health)+" / "+Mathf.CeilToInt(type.health)+" HP";
                browserDescription.text=!entity.complete?Localization.Text("Under construction")+" · "+Mathf.FloorToInt(entity.progress*100)+"%":
                    Localization.Text(type.building?BuildingRole(type.role):UnitVisuals.RoleName(type.role))+" · "+Localization.Text(OrderLabel(entity.order));
                if(type.role=="caster")((LocalizedText)browserDescription).Append("\n"+Session.AbilityStatus(type.abilities.FirstOrDefault()??""));
            }
            else if(selected.Length>1)
            {
                browserPortrait.SetType(Session.Catalog[selected[0].definition]);
                ((LocalizedText)browserName).SetKey("Selected: {0}",selected.Length);
                browserDescription.text=string.Join(" · ",selected.GroupBy(e=>e.definition).Take(4).Select(g=>g.Count()+" × "+Localization.Text(Session.Catalog[g.Key].name)));
                browserHealth.text=Mathf.CeilToInt(selected.Sum(e=>e.health))+" HP";
            }
            else
            {
                var resource=view.resources.FirstOrDefault(r=>r.id==Session.SelectedResource);
                browserName.text=resource==null?"No selection":resource.kind=="mineral"?"Minerals":"Gas";
                browserDescription.text=resource==null?"Select a unit or building to see its name and available actions.":resource.remaining+" "+Localization.Text("remaining");
            }
            if(browserMessageVersion!=Session.MessageVersion){browserMessageVersion=Session.MessageVersion;browserMessageUntil=Time.unscaledTime+8;}
            status.text=Time.unscaledTime<browserMessageUntil?Session.Message:"";status.color=Session.MessageIsError?ErrorTint:ConsoleTheme.Ink;
            statusBanner.GetComponent<Image>().color=string.IsNullOrEmpty(status.text)?Color.clear:ConsoleTheme.Background;
            var available=SelectionActions.For(Session);
            foreach(var target in targets.Where(t=>t.actionId!=null))
            {
                var current=available.Items.FirstOrDefault(a=>a.Id==target.actionId);
                if(current!=null){target.help=current;target.detail.text=MatchActionDetail(current,false);}
            }
            RefreshBrowserQueues();UpdateGroupFeedback();
        }

        private void RefreshBrowserQueues()
        {
            if(Page!=MenuPage.Actions||browserQueue==null)return;
            var producers=SelectedMobileUnits().Length>0?Array.Empty<VisibleEntity>():SelectedProducers();
            string key=browserQueue.rect.width+":"+OrderPlanSignature()+":"+string.Join(";",producers.Select(p=>p.id+":"+string.Join(",",p.queue.Select(q=>q.id))));
            if(key==browserQueueSignature){UpdateBrowserProduction(producers);return;}browserQueueSignature=key;
            foreach(var target in browserQueueTargets){targets.Remove(target);if(pressed==target)pressed=null;if(hovered==target)hovered=null;}
            browserQueueTargets.Clear();
            browserProductionItems.Clear();
            foreach(Transform child in browserQueue){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var match=Session.SessionIdentity;var ids=Session.Selection.OrderBy(id=>id).ToArray();
            if(producers.Length>0)
            {
                browserQueueTitle.text="PRODUCTION · Click an item to cancel and refund";
                var queue=VisibleProduction(producers);float width=(browserQueue.rect.width-16)/5;
                for(int i=0;i<queue.Length;i++)
                {
                    var item=queue[i];if(item.order==null||item.order.id<=0)continue;
                    int producer=producers[item.producer-1].id,orderId=item.order.id;
                    var target=BrowserButton(browserQueue,QueueLabel(item.order.definition),new Vector2(i*(width+4),0),new Vector2(width,54),()=>
                    {
                        if(!AcceptsGameInput||Session.SessionIdentity!=match||!Session.Selection.OrderBy(id=>id).SequenceEqual(ids)
                            ||!SelectedProducers().Any(p=>p.id==producer&&p.queue.Any(q=>q.id==orderId)))return;
                        if(Session.Send(CommandKind.Cancel,queueOrderId:orderId,entityIds:new[]{producer}))
                            Session.Announce(Session.IsOnline?"Cancellation requested.":QueueLabel(item.order.definition)+" cancelled. Resources refunded.");
                        nextText=0;
                    });
                    var name=target.rect.GetComponentInChildren<Text>();name.fontSize=13;
                    TopLeft(name.rectTransform,new Vector2(5,-2),new Vector2(width-10,28));
                    var state=Label(target.rect,"",new Vector2(5,-31),new Vector2(width-10,17),11,ConsoleTheme.Muted,TextAnchor.MiddleCenter);
                    Section(target.rect,"Progress track",new Vector2(5,-49),new Vector2(width-10,3),ConsoleTheme.Track);
                    var fill=Section(target.rect,"Progress",new Vector2(5,-49),new Vector2(0,3),ConsoleTheme.Accent).GetComponent<Image>();
                    browserProductionItems.Add((producer,orderId,item.depth,state,fill,width-10));
                    browserQueueTargets.Add(target);targets.Add(target);
                }
                UpdateBrowserProduction(producers);
            }
            else
            {
                var orders=DisplayedUnitOrders(out var representative);
                browserQueueTitle.text=orders.Length>0?"QUEUED ORDERS · Click an item to remove":"Left-click: select · Right-click: order · Shift: queue";
                var signatureAtBind=OrderPlanSignature();
                int count=orders.Length+(orders.Length>1?1:0),columns=Mathf.Min(6,Mathf.Max(1,count));
                float width=(browserQueue.rect.width-(columns-1)*4)/columns;
                for(int i=0;i<count;i++)
                {
                    bool clear=i==orders.Length;var order=clear?null:orders[i];
                    int[] affected=SelectedMobileUnits().Where(e=>clear||(order.sourceSequence==0?e.id==representative.id:
                        e.order!=CommandKind.Stop&&e.activeOrderSequence==order.sourceSequence||e.pendingOrders.Any(q=>q.sourceSequence==order.sourceSequence)))
                        .Select(e=>e.id).ToArray();
                    var target=BrowserButton(browserQueue,clear?"Cancel all":PlanOrderLabel(order.kind),new Vector2(i%columns*(width+4),-i/columns*29),new Vector2(width,26),()=>
                    {
                        if(!AcceptsGameInput||Session.SessionIdentity!=match||OrderPlanSignature()!=signatureAtBind
                            ||!Session.Selection.OrderBy(id=>id).SequenceEqual(ids))return;
                        if(Session.Send(clear?CommandKind.Stop:CommandKind.CancelUnitOrder,unitOrderSequence:clear?0:order.sourceSequence,entityIds:affected))
                            Session.Announce(Session.IsOnline?"Cancellation requested.":clear?"Current and queued orders cancelled.":"Unit order cancelled.");
                        nextText=0;
                    });
                    target.rect.GetComponentInChildren<Text>().fontSize=12;browserQueueTargets.Add(target);targets.Add(target);
                    target.normal=clear?ConsoleTheme.Danger:i==0?QueueActiveTint:button;target.image.color=target.normal;
                }
            }
        }

        private void UpdateBrowserProduction(VisibleEntity[] producers)
        {
            foreach(var item in browserProductionItems)
            {
                var producer=producers.FirstOrDefault(p=>p.id==item.producer);
                var order=producer?.queue.FirstOrDefault(q=>q.id==item.order);if(order==null)continue;
                bool active=item.depth==0,waiting=active&&order.remaining<=0&&!order.research;
                float progress=active?QueueProgress(order):0;
                string state=!active?Localization.Text("QUEUED"):Session.IsPaused&&!Session.IsOnline?Localization.Text("PAUSED"):
                    waiting?Localization.Text("READY"):$"{Mathf.FloorToInt(progress*100)}% · {Mathf.CeilToInt(Mathf.Max(0,order.remaining)/(float)Simulation.TickRate)}s";
                item.state.text=(producers.Length>1?"B"+(Array.IndexOf(producers,producer)+1)+" · ":"")+state;
                var tint=active?((Session.IsPaused&&!Session.IsOnline)||waiting?WarningTint:ConsoleTheme.Accent):ConsoleTheme.Muted;
                item.state.color=item.fill.color=tint;item.fill.rectTransform.sizeDelta=new Vector2(item.width*progress,3);
            }
        }

        private void LayoutBrowserActionHelp()
        {
            if(!actionHelp.gameObject.activeSelf||SectionHelpOpen)return;
            const float width=470,height=224;
            TopLeft(actionHelp,new Vector2(root.rect.width-width-12,-root.rect.height+BrowserDockHeight+height+24),new Vector2(width,height));
            TopLeft(helpCategory.rectTransform,new Vector2(16,-12),new Vector2(width-32,22));helpCategory.fontSize=13;
            TopLeft(helpTitle.rectTransform,new Vector2(16,-40),new Vector2(width-32,30));helpTitle.fontSize=22;
            TopLeft(helpDescription.rectTransform,new Vector2(16,-78),new Vector2(width-32,88));helpDescription.fontSize=16;
            TopLeft(helpDetail.rectTransform,new Vector2(16,-174),new Vector2(width-32,42));helpDetail.fontSize=14;
            actionHelp.SetAsLastSibling();
        }

        private void PositionBrowserTutorial()
        {
            var size=((RectTransform)canvas.transform).rect.size;
            float scale=Mathf.Min(.68f,(size.x-32)/TutorialCardWidth);
            bool briefing=Session.Tutorial.Briefing||Session.Tutorial.Complete;
            float width=tutorialCard.rect.width*scale;
            // A fixed screen card leaves both the battlefield and bottom command
            // dock available. World targets retain the shared violet map marker.
            TopLeft(tutorialCard,new Vector2(briefing?(size.x-width)*.5f:16,-84),tutorialCard.sizeDelta);
            tutorialCard.localScale=Vector3.one*scale;tutorialCard.localRotation=Quaternion.identity;
            tutorialCard.SetAsLastSibling();
            tutorialMenuDim.gameObject.SetActive(false);
            UpdateBrowserTutorialHighlight();
            string state=Session.Tutorial.Step+":"+Session.Tutorial.Briefing;
            if(state!=tutorialAttentionState)
            {
                tutorialAttentionState=state;
                if(Application.isPlaying&&!Session.HasSystemPause)Session.GetComponent<AudioFeedback>()?.TutorialTransition();
            }
        }

        private void UpdateBrowserTutorialHighlight()
        {
            bool show=TutorialCardVisible&&!Session.Tutorial.Briefing&&tutorialCueTargetRect!=null;
            for(int i=0;i<4;i++)
            {
                if(browserTutorialEdges[i]==null&&show)
                {
                    browserTutorialEdges[i]=Section((RectTransform)canvas.transform,"Tutorial target edge",Vector2.zero,Vector2.one,ConsoleTheme.Accent);
                    browserTutorialEdges[i].GetComponent<Image>().sprite=null;
                }
                if(browserTutorialEdges[i]!=null)browserTutorialEdges[i].gameObject.SetActive(show);
            }
            if(!show)return;
            var corners=new Vector3[4];tutorialCueTargetRect.GetWorldCorners(corners);
            var parent=(RectTransform)canvas.transform;
            Vector2 lower=parent.InverseTransformPoint(corners[0]),upper=parent.InverseTransformPoint(corners[2]);
            float x=lower.x-parent.rect.xMin-4,y=upper.y-parent.rect.yMax+4,w=upper.x-lower.x+8,h=upper.y-lower.y+8;
            TopLeft(browserTutorialEdges[0],new Vector2(x,y),new Vector2(w,3));
            TopLeft(browserTutorialEdges[1],new Vector2(x,y-h+3),new Vector2(w,3));
            TopLeft(browserTutorialEdges[2],new Vector2(x,y),new Vector2(3,h));
            TopLeft(browserTutorialEdges[3],new Vector2(x+w-3,y),new Vector2(3,h));
            foreach(var edge in browserTutorialEdges)edge.SetAsLastSibling();
        }
    }
}
#endif
