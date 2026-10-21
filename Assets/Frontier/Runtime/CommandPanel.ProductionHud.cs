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
        private Image productionFill, productionAccent, productionCard;
        private SelectionPortrait productionPortrait;
        private readonly SelectionPortrait[] queuedPortraits=new SelectionPortrait[4];
        private readonly Text[] queuedNames=new Text[4], queuedKinds=new Text[4], queuedPositions=new Text[4];
        private readonly Image[] queuedCards=new Image[4];
        private readonly Image[] queuedProgress=new Image[4];
        private readonly Target[] productionTargets=new Target[5];
        private readonly RectTransform[] cancelHints=new RectTransform[5];
        private static readonly Color QueueIdleTint=new Color(.52f,.65f,.72f);
        private static readonly Color QueueCancelTint=new Color(.65f,.08f,.1f);

        private void CreateProductionHud()
        {
            productionStrip=Section(matchHud,"Production queue",new Vector2(24,-422),new Vector2(1052,168));
            productionTitle=Label(productionStrip,"PRODUCTION QUEUE",new Vector2(16,-8),new Vector2(350,26),18,accent);
            productionHint=Label(productionStrip,"CLICK AN ITEM TO CANCEL",new Vector2(380,-8),new Vector2(370,26),15,QueueIdleTint);
            productionCount=Label(productionStrip,"",new Vector2(802,-8),new Vector2(234,26),20,Color.white,TextAnchor.MiddleRight);

            var active=Section(productionStrip,"Current production",new Vector2(14,-42),new Vector2(352,112),new Color(.055f,.18f,.22f));
            productionCard=active.GetComponent<Image>();
            productionAccent=Section(active,"Active accent",Vector2.zero,new Vector2(4,112),accent).GetComponent<Image>();
            productionState=Label(active,"",new Vector2(16,-4),new Vector2(320,22),15,accent);
            productionName=Label(active,"",new Vector2(16,-25),new Vector2(320,38),28,Color.white);
            productionPortrait=ModelPortrait(active,new Vector2(16,-26),new Vector2(44,38));
            var bar=Section(active,"Production progress track",new Vector2(16,-68),new Vector2(320,12),new Color(.12f,.28f,.31f));
            productionFill=Section(bar,"Production progress",Vector2.zero,new Vector2(0,12),accent).GetComponent<Image>();
            productionProgress=Label(active,"",new Vector2(16,-84),new Vector2(320,24),19,Color.white);
            cancelHints[0]=CreateCancelHint(active,352);
            for(int i=0;i<4;i++)
            {
                var card=Section(productionStrip,"Queue position "+(i+2),new Vector2(380+i*166,-42),new Vector2(160,112));
                queuedCards[i]=card.GetComponent<Image>();
                queuedPositions[i]=Label(card,(i+2).ToString(),new Vector2(12,-5),new Vector2(136,22),16,QueueIdleTint);
                queuedNames[i]=Label(card,"",new Vector2(12,-30),new Vector2(136,52),21,Color.white);
                queuedPortraits[i]=ModelPortrait(card,new Vector2(8,-30),new Vector2(48,48));
                queuedProgress[i]=Section(card,"Production progress",new Vector2(12,-82),new Vector2(0,3),accent).GetComponent<Image>();
                queuedKinds[i]=Label(card,"",new Vector2(12,-87),new Vector2(136,22),14,QueueIdleTint);
                cancelHints[i+1]=CreateCancelHint(card,160);
            }
        }

        private RectTransform CreateCancelHint(RectTransform card,float width)
        {
            var hint=Section(card,"Cancel hint",new Vector2(8,-82),new Vector2(width-16,30),QueueCancelTint);
            Label(hint,"Cancel + refund",new Vector2(4,0),new Vector2(width-24,30),14,Color.white,TextAnchor.MiddleCenter);
            hint.gameObject.SetActive(false);return hint;
        }
        private void ClearProductionTargets()
        {
            for(int i=0;i<productionTargets.Length;i++)
            {
                var target=productionTargets[i];
                if(target!=null)target.image.color=target.normal;
                productionTargets[i]=null;
                if(cancelHints[i]!=null)cancelHints[i].gameObject.SetActive(false);
            }
        }
        private void BindProductionTargets()
        {
            var producer=SelectedProducer();if(producer==null)return;
            var match=Session.Match;int producerId=producer.id;
            for(int i=0;i<producer.queue.Length&&i<productionTargets.Length;i++)
            {
                var order=producer.queue[i];int orderId=order.id;
                if(orderId<=0)continue;
                var card=i==0?productionCard:queuedCards[i-1];
                var target=new Target{rect=card.rectTransform,image=card,normal=i==0?new Color(.055f,.18f,.22f):button,hoverColor=QueueCancelTint,
                    click=()=>
                    {
                        if(!AcceptsGameInput||Session.Match!=match||SelectedProducer()?.id!=producerId)return;
                        if(Session.Send(CommandKind.Cancel,queueOrderId:orderId))Session.Announce(QueueLabel(order.definition)+" cancelled. Resources refunded.");
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
                if(target!=null)target.image.color=active?QueueCancelTint:target.normal;
            }
        }

        private VisibleEntity SelectedProducer()
        {
            if(Session.Selection.Count!=1)return null;
            return SelectedProducers().FirstOrDefault();
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
            productionTitle.text="PRODUCTION QUEUE";
            productionHint.text="CLICK AN ITEM TO CANCEL";
            var queue=producer.queue;
            productionCount.text=$"{queue.Length} / 5"+(queue.Length>=5?" · FULL":"");
            productionCount.color=queue.Length>=5?WarningTint:Color.white;
            UpdateCurrentProduction(queue.FirstOrDefault());
            for(int i=0;i<4;i++)
            {
                queuedPositions[i].text=(i+2).ToString();
                bool occupied=i+1<queue.Length;
                var item=occupied?queue[i+1]:null;
                UpdateQueuedPortrait(i,item);
                queuedNames[i].text=occupied?QueueLabel(item.definition):"Empty";
                queuedNames[i].color=occupied?Color.white:QueueIdleTint;
                queuedKinds[i].text=occupied?(item.research?"Research":UnitVisuals.RoleName(Session.Catalog[item.definition].role)):"Available slot";
                queuedKinds[i].color=QueueIdleTint;
                queuedCards[i].color=occupied?button:new Color(.035f,.075f,.105f);
                queuedProgress[i].rectTransform.sizeDelta=new Vector2(0,3);
            }
            UpdateProductionHover();
        }

        private static float QueueProgress(ProductionOrder order)=>order.total>0?Mathf.Clamp01(1-order.remaining/(float)order.total):1;

        private void UpdateCurrentProduction(ProductionOrder current,int producerNumber=0)
        {
            bool portrait=UpdateProductionPortrait(productionPortrait,current);
            TopLeft(productionName.rectTransform,new Vector2(portrait?72:16,-25),new Vector2(portrait?264:320,38));
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
                if(Session.IsPaused)
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
            if(producerNumber>0&&current!=null)productionState.text+=" · B"+producerNumber;
            if(producerNumber>0&&current==null&&Session.IsPaused){productionState.text="PAUSED";tint=WarningTint;}
            productionAccent.color=productionFill.color=productionState.color=tint;
        }

        private void UpdateGroupProductionHud(VisibleEntity[] producers)
        {
            int queued=producers.Sum(e=>e.queue.Length),capacity=producers.Length*5;
            int full=producers.Count(e=>e.queue.Length>=5);
            productionTitle.text="GROUP PRODUCTION";
            productionHint.text=producers.Length+(producers.Length==1?" BUILDING":" BUILDINGS")+" · SELECT ONE TO CANCEL";
            productionCount.text=$"{queued} / {capacity}"+(full==producers.Length?" · FULL":"");
            productionCount.color=full==producers.Length?WarningTint:Color.white;
            // Show parallel work first, then each producer's pending orders in FIFO
            // order. Every visible slot is an actual order, including duplicates.
            var visible=new (ProductionOrder order,int producer,int depth)[5];int count=0;
            for(int depth=0;depth<5&&count<visible.Length;depth++)
                for(int p=0;p<producers.Length&&count<visible.Length;p++)
                    if(depth<producers[p].queue.Length)visible[count++]=(producers[p].queue[depth],p+1,depth);
            UpdateCurrentProduction(visible[0].order,count>0?visible[0].producer:1);
            for(int i=0;i<4;i++)
            {
                var item=visible[i+1];bool occupied=item.order!=null,active=occupied&&item.depth==0;
                UpdateQueuedPortrait(i,item.order);
                float progress=active?QueueProgress(item.order):0;
                bool waiting=active&&item.order.remaining<=0&&!item.order.research;
                var tint=active?(Session.IsPaused||waiting?WarningTint:accent):QueueIdleTint;
                queuedPositions[i].text=i==3&&queued>5?$"5 · +{queued-5} MORE":(i+2).ToString();
                queuedNames[i].text=occupied?QueueLabel(item.order.definition):"Empty";
                queuedNames[i].color=occupied?Color.white:QueueIdleTint;
                string state=!active?"QUEUED":Session.IsPaused?"PAUSED":waiting?"READY":$"{Mathf.FloorToInt(progress*100)}%";
                queuedKinds[i].text=occupied?$"B{item.producer} · {state}":"Available slot";
                queuedKinds[i].color=tint;
                queuedCards[i].color=occupied?button:new Color(.035f,.075f,.105f);
                queuedProgress[i].rectTransform.sizeDelta=new Vector2(136*progress,3);queuedProgress[i].color=tint;
            }
            // Group cancellation remains an explicit single-building operation.
            UpdateProductionHover();
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
            TopLeft(queuedNames[index].rectTransform,new Vector2(portrait?62:12,-30),new Vector2(portrait?90:136,52));
            queuedNames[index].fontSize=portrait?17:21;
        }
    }
}
