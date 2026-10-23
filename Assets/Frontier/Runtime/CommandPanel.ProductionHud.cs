using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private RectTransform productionStrip;
        private Text productionCount, productionState, productionName, productionProgress, productionTitle, productionHint;
        private Image productionFill, productionCard;
        private SelectionPortrait productionPortrait;
        private readonly SelectionPortrait[] queuedPortraits=new SelectionPortrait[4];
        private readonly Text[] queuedNames=new Text[4], queuedKinds=new Text[4], queuedPositions=new Text[4];
        private readonly Image[] queuedCards=new Image[4];
        private readonly Image[] queuedActiveBorders=new Image[4];
        private readonly Image[] queuedProgress=new Image[4];
        private readonly Target[] productionTargets=new Target[5];
        private readonly RectTransform[] cancelHints=new RectTransform[5];
        private static readonly Color QueueIdleTint=ConsoleTheme.Muted;
        private static readonly Color QueueActiveTint=new Color(51/255f,108/255f,82/255f).linear;
        private static readonly Color QueueActiveBorderTint=new Color(113/255f,202/255f,143/255f).linear;
        private static readonly Color QueueOverflowBorderTint=new Color(240/255f,212/255f,103/255f).linear;

        private void CreateProductionHud()
        {
            productionStrip=Section(matchHud,"Production queue",new Vector2(24,-422),new Vector2(HudInnerWidth,HudContentTop+136+HudPadding));
            productionTitle=Label(productionStrip,"PRODUCTION",new Vector2(HudPadding,-HudPadding),new Vector2(350,28),20,ConsoleTheme.Muted);
            productionTitle.fontStyle=FontStyle.Bold;
            productionHint=Label(productionStrip,"CLICK AN ITEM TO CANCEL",new Vector2(380,-8),new Vector2(370,26),15,QueueIdleTint);
            productionHint.gameObject.SetActive(false);
            productionCount=Label(productionStrip,"",new Vector2(1130,-HudPadding),new Vector2(190,28),20,ConsoleTheme.Ink,TextAnchor.MiddleRight);
            productionCount.fontStyle=FontStyle.Bold;

            var active=Section(productionStrip,"Current production",new Vector2(HudPadding,-HudContentTop),new Vector2(368,136),QueueActiveTint);
            productionCard=active.GetComponent<Image>();
            productionState=Label(active,"",new Vector2(24,-8),new Vector2(320,24),17,accent);
            productionName=Label(active,"",new Vector2(24,-34),new Vector2(320,56),20,ConsoleTheme.Ink);
            productionName.fontStyle=FontStyle.Bold;
            productionPortrait=ModelPortrait(active,new Vector2(24,-38),new Vector2(48,48));
            var bar=Section(active,"Production progress track",new Vector2(24,-94),new Vector2(320,12),ConsoleTheme.Track);
            productionFill=Section(bar,"Production progress",Vector2.zero,new Vector2(0,12),accent).GetComponent<Image>();
            productionProgress=Label(active,"",new Vector2(24,-108),new Vector2(320,24),19,ConsoleTheme.Ink);
            cancelHints[0]=CreateCancelHint(active,368);
            for(int i=0;i<4;i++)
            {
                var card=Section(productionStrip,"Queue position "+(i+2),new Vector2(408+i*250,-HudContentTop),new Vector2(234,136),button);
                queuedCards[i]=card.GetComponent<Image>();
                // Native rounded fills form a solid 4 px edge without affecting
                // labels, cancellation targets or the card's outer dimensions.
                var border=Section(card,"Active producer border",Vector2.zero,card.sizeDelta,QueueActiveBorderTint);
                queuedActiveBorders[i]=border.GetComponent<Image>();
                Section(border,"Card interior",new Vector2(4,-4),card.sizeDelta-Vector2.one*8,button);
                border.gameObject.SetActive(false);
                queuedPositions[i]=Label(card,(i+2).ToString(),new Vector2(16,-12),new Vector2(202,22),17,QueueIdleTint);
                queuedNames[i]=Label(card,"",new Vector2(16,-36),new Vector2(202,52),20,ConsoleTheme.Ink,TextAnchor.MiddleLeft);
                queuedNames[i].fontStyle=FontStyle.Bold;
                queuedPortraits[i]=ModelPortrait(card,new Vector2(16,-40),new Vector2(48,48));
                Section(card,"Queue progress track",new Vector2(49,-96),new Vector2(136,3),ConsoleTheme.Track);
                queuedProgress[i]=Section(card,"Production progress",new Vector2(49,-96),new Vector2(0,3),accent).GetComponent<Image>();
                queuedKinds[i]=Label(card,"",new Vector2(16,-104),new Vector2(202,24),17,QueueIdleTint,TextAnchor.MiddleCenter);
                cancelHints[i+1]=CreateCancelHint(card,234);
            }
        }

        private RectTransform CreateCancelHint(RectTransform card,float width)
        {
            var hint=Section(card,"Cancel hint",new Vector2(8,-100),new Vector2(width-16,28),ConsoleTheme.Accent);
            Label(hint,"Cancel + refund",new Vector2(4,0),new Vector2(width-24,28),17,ConsoleTheme.HoverInk,TextAnchor.MiddleCenter);
            hint.gameObject.SetActive(false);return hint;
        }
        private void ClearProductionTargets()
        {
            for(int i=0;i<productionTargets.Length;i++)
            {
                var target=productionTargets[i];
                ApplyTargetHover(target,false);
                productionTargets[i]=null;
                if(cancelHints[i]!=null)cancelHints[i].gameObject.SetActive(false);
                if(i>0&&queuedActiveBorders[i-1]!=null)queuedActiveBorders[i-1].gameObject.SetActive(false);
            }
        }
        private void BindProductionTargets()
        {
            var producers=SelectedProducers();if(producers.Length==0)return;
            var visible=VisibleProduction(producers);
            var match=Session.SessionIdentity;var selectionIds=Session.Selection.OrderBy(id=>id).ToArray();
            for(int i=0;i<visible.Length;i++)
            {
                var item=visible[i];var order=item.order;if(order==null)continue;
                int orderId=order.id,producerId=producers[item.producer-1].id;
                if(orderId<=0)continue;
                var card=i==0?productionCard:queuedCards[i-1];
                var target=new Target{rect=card.rectTransform,image=card,normal=i==0?QueueActiveTint:button,
                    click=()=>
                    {
                        if(!AcceptsGameInput||Session.SessionIdentity!=match||!Session.Selection.OrderBy(id=>id).SequenceEqual(selectionIds)
                            ||!SelectedProducers().Any(p=>p.id==producerId&&p.queue.Any(q=>q.id==orderId)))return;
                        if(Session.Send(CommandKind.Cancel,queueOrderId:orderId,entityIds:new[]{producerId}))Session.Announce(Session.IsOnline?"Cancellation requested.":QueueLabel(order.definition)+" cancelled. Resources refunded.");
                        nextText=0;
                    }};
                productionTargets[i]=target;targets.Add(target);
            }
        }
        private void UpdateProductionHover()
        {
            for(int i=0;i<productionTargets.Length;i++)
            {
                var target=productionTargets[i];
                bool active=target!=null&&hovered==target&&AcceptsGameInput&&!Quest.Placing&&target.rect.gameObject.activeInHierarchy;
                if(cancelHints[i]!=null)cancelHints[i].gameObject.SetActive(active);
                ApplyTargetHover(target,active);
                if(i>0&&queuedActiveBorders[i-1]!=null)
                    queuedActiveBorders[i-1].transform.GetChild(0).GetComponent<Image>().color=active?ConsoleTheme.Accent:button;
            }
        }

        private VisibleEntity[] SelectedProducers()
        {
            if(Session.View==null||Session.Selection.Count==0)return System.Array.Empty<VisibleEntity>();
            var selected=Session.View.entities.Where(e=>Session.Selection.Contains(e.id)).OrderBy(e=>e.id).ToArray();
            if(selected.Length!=Session.Selection.Count||selected.Any(e=>e.owner!=Session.View.player||e.remembered||e.health<=0||
                !Session.Catalog[e.definition].building))return System.Array.Empty<VisibleEntity>();
            return selected.Where(e=>e.complete&&Session.Catalog.CanProduceUnits(e.definition)).ToArray();
        }

        private void UpdateProductionHud()
        {
            var producers=SelectedProducers();
            if(producers.Length==0)return;
            if(Session.Selection.Count>1){UpdateGroupProductionHud(producers);return;}
            var producer=producers[0];
            productionTitle.text="PRODUCTION";
            productionHint.text="Click any item, including the current one, to cancel and refund it. The next item advances automatically.";
            var queue=producer.queue;
            productionCount.text=$"{queue.Length} / 5"+(queue.Length>=5?" · FULL":"");
            productionCount.color=queue.Length>=5?WarningTint:ConsoleTheme.Ink;
            UpdateCurrentProduction(queue.FirstOrDefault());
            for(int i=0;i<4;i++)
            {
                queuedPositions[i].text=(i+2).ToString();
                bool occupied=i+1<queue.Length;
                var item=occupied?queue[i+1]:null;
                UpdateQueuedPortrait(i,item);
                queuedNames[i].text=occupied?QueueLabel(item.definition):"Empty";
                queuedNames[i].color=occupied?ConsoleTheme.Ink:QueueIdleTint;
                queuedKinds[i].text=occupied?(item.research?"Research":UnitVisuals.RoleName(Session.Catalog[item.definition].role)):"Available slot";
                queuedKinds[i].color=QueueIdleTint;
                queuedCards[i].color=button;
                queuedActiveBorders[i].color=QueueActiveBorderTint;
                queuedActiveBorders[i].gameObject.SetActive(i==3&&occupied);
                queuedProgress[i].rectTransform.sizeDelta=new Vector2(0,3);
            }
            UpdateProductionHover();
        }

        private static float QueueProgress(ProductionOrder order)=>order.total>0?Mathf.Clamp01(1-order.remaining/(float)order.total):1;

        private void UpdateCurrentProduction(ProductionOrder current,int producerNumber=0)
        {
            bool portrait=UpdateProductionPortrait(productionPortrait,current);
            TopLeft(productionName.rectTransform,new Vector2(portrait?84:24,-34),new Vector2(portrait?260:320,56));
            var tint=accent;
            if(current==null)
            {
                productionState.text="IDLE";productionName.text="Queue empty";
                productionProgress.text="Choose a Train or Research action";
                productionFill.rectTransform.sizeDelta=new Vector2(0,12);tint=QueueIdleTint;
            }
            else
            {
                float progress=QueueProgress(current);
                int seconds=Mathf.CeilToInt(Mathf.Max(0,current.remaining)/(float)Simulation.TickRate);
                productionName.text=QueueLabel(current.definition);
                productionState.text=current.research?"1 · RESEARCHING":"1 · TRAINING";
                productionProgress.text=$"{Mathf.FloorToInt(progress*100)}% · {seconds}s remaining";
                if(Session.IsPaused&&!Session.IsOnline)
                {
                    productionState.text="1 · PAUSED";tint=WarningTint;
                }
                else if(current.remaining<=0&&!current.research)
                {
                    // The global supply counter includes reservations. Only existing units
                    // block deployment after training; never mistake a reserved slot for one.
                    int units=Session.View.entities.Count(e=>e.owner==Session.View.player&&!Session.Catalog[e.definition].building);
                    productionState.text="1 · WAITING TO DEPLOY";
                    productionProgress.text=units>=Session.View.capacity?"100% · Needs more supply":"100% · Clear the building exit";
                    tint=WarningTint;
                }
                productionFill.rectTransform.sizeDelta=new Vector2(320*progress,12);
            }
            if(producerNumber>0&&current!=null)((LocalizedText)productionState).Append(" · B"+producerNumber);
            if(producerNumber>0&&current==null&&Session.IsPaused&&!Session.IsOnline){productionState.text="PAUSED";tint=WarningTint;}
            productionFill.color=productionState.color=tint;
        }

        private void UpdateGroupProductionHud(VisibleEntity[] producers)
        {
            int queued=producers.Sum(e=>e.queue.Length),capacity=producers.Length*5;
            int full=producers.Count(e=>e.queue.Length>=5);
            productionTitle.text="PRODUCTION";
            productionHint.text="Click any item, including the current one, to cancel and refund it. The next item advances automatically.";
            productionCount.text=$"{queued} / {capacity}"+(full==producers.Length?" · FULL":"");
            productionCount.color=full==producers.Length?WarningTint:ConsoleTheme.Ink;
            // Show parallel work first, then each producer's pending orders in FIFO
            // order. Every visible slot is an actual order, including duplicates.
            var visible=VisibleProduction(producers);int count=visible.Count(item=>item.order!=null);
            UpdateCurrentProduction(visible[0].order,count>0?visible[0].producer:1);
            for(int i=0;i<4;i++)
            {
                var item=visible[i+1];bool occupied=item.order!=null,active=occupied&&item.depth==0;
                UpdateQueuedPortrait(i,item.order);
                float progress=active?QueueProgress(item.order):0;
                bool waiting=active&&item.order.remaining<=0&&!item.order.research;
                var tint=active?((Session.IsPaused&&!Session.IsOnline)||waiting?WarningTint:accent):QueueIdleTint;
                queuedPositions[i].text=i==3&&queued>5?$"5 · +{queued-5} MORE":(i+2).ToString();
                queuedNames[i].text=occupied?QueueLabel(item.order.definition):"Empty";
                queuedNames[i].color=occupied?ConsoleTheme.Ink:QueueIdleTint;
                string state=!active?"QUEUED":Session.IsPaused&&!Session.IsOnline?"PAUSED":waiting?"READY":$"{Mathf.FloorToInt(progress*100)}%";
                queuedKinds[i].text=occupied?$"B{item.producer} · {state}":"Available slot";
                queuedKinds[i].color=tint;
                queuedCards[i].color=button;
                queuedActiveBorders[i].color=i==3&&queued>5?QueueOverflowBorderTint:QueueActiveBorderTint;
                queuedActiveBorders[i].gameObject.SetActive(active||(i==3&&occupied));
                queuedProgress[i].rectTransform.sizeDelta=new Vector2(136*progress,3);queuedProgress[i].color=tint;
            }
            // Each visible card addresses its exact producer and production ID.
            UpdateProductionHover();
        }
        private static (ProductionOrder order,int producer,int depth)[] VisibleProduction(VisibleEntity[] producers)
        {
            var visible=new (ProductionOrder order,int producer,int depth)[5];int count=0;
            for(int depth=0;depth<5&&count<visible.Length;depth++)
                for(int p=0;p<producers.Length&&count<visible.Length;p++)
                    if(depth<producers[p].queue.Length)visible[count++]=(producers[p].queue[depth],p+1,depth);
            return visible;
        }
        private bool UpdateProductionPortrait(SelectionPortrait portrait,ProductionOrder order)
        {
            bool visible=order!=null&&!order.research;
            if(visible)portrait.SetType(Session.Catalog[order.definition]);
            portrait.gameObject.SetActive(visible);return visible;
        }
        private void UpdateQueuedPortrait(int index,ProductionOrder order)
        {
            bool portrait=UpdateProductionPortrait(queuedPortraits[index],order);
            TopLeft(queuedNames[index].rectTransform,new Vector2(portrait?76:16,-36),new Vector2(portrait?142:202,52));
        }
    }
}
