using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private RectTransform productionStrip;
        private Text productionCount, productionState, productionName, productionProgress;
        private Image productionFill, productionAccent;
        private readonly Text[] queuedNames=new Text[4], queuedKinds=new Text[4];
        private readonly Image[] queuedCards=new Image[4];
        private static readonly Color QueueIdleTint=new Color(.52f,.65f,.72f);

        private void CreateProductionHud()
        {
            productionStrip=Section(matchHud,"Production queue",new Vector2(24,-422),new Vector2(1052,168));
            Label(productionStrip,"PRODUCTION QUEUE",new Vector2(16,-8),new Vector2(350,26),18,accent);
            Label(productionStrip,"NEXT IN ORDER  →",new Vector2(380,-8),new Vector2(370,26),15,QueueIdleTint);
            productionCount=Label(productionStrip,"",new Vector2(802,-8),new Vector2(234,26),20,Color.white,TextAnchor.MiddleRight);

            var active=Section(productionStrip,"Current production",new Vector2(14,-42),new Vector2(352,112),new Color(.055f,.18f,.22f));
            productionAccent=Section(active,"Active accent",Vector2.zero,new Vector2(4,112),accent).GetComponent<Image>();
            productionState=Label(active,"",new Vector2(16,-4),new Vector2(320,22),15,accent);
            productionName=Label(active,"",new Vector2(16,-25),new Vector2(320,38),28,Color.white);
            var bar=Section(active,"Production progress track",new Vector2(16,-68),new Vector2(320,12),new Color(.12f,.28f,.31f));
            productionFill=Section(bar,"Production progress",Vector2.zero,new Vector2(0,12),accent).GetComponent<Image>();
            productionProgress=Label(active,"",new Vector2(16,-84),new Vector2(320,24),19,Color.white);
            for(int i=0;i<4;i++)
            {
                var card=Section(productionStrip,"Queue position "+(i+2),new Vector2(380+i*166,-42),new Vector2(160,112));
                queuedCards[i]=card.GetComponent<Image>();
                Label(card,(i+2).ToString(),new Vector2(12,-5),new Vector2(136,22),16,QueueIdleTint);
                queuedNames[i]=Label(card,"",new Vector2(12,-30),new Vector2(136,52),21,Color.white);
                queuedKinds[i]=Label(card,"",new Vector2(12,-87),new Vector2(136,22),14,QueueIdleTint);
            }
        }

        private VisibleEntity SelectedProducer()
        {
            if(Session.View==null||Session.Selection.Count!=1)return null;
            var entity=Session.View.entities.FirstOrDefault(e=>Session.Selection.Contains(e.id));
            if(entity==null||entity.owner!=Session.View.player||entity.remembered||!entity.complete)return null;
            var type=Session.Catalog[entity.definition];
            if(!type.building)return null;
            return type.role=="tech"||Session.Catalog.Definition.archetypes.Any(t=>!t.building&&t.faction==type.faction&&t.producer==type.role)?entity:null;
        }

        private void UpdateProductionHud()
        {
            var producer=SelectedProducer();
            if(producer==null)return;
            var queue=producer.queue;
            productionCount.text=$"{queue.Length} / 5"+(queue.Length>=5?" · FULL":"");
            productionCount.color=queue.Length>=5?WarningTint:Color.white;
            var tint=accent;
            if(queue.Length==0)
            {
                productionState.text="IDLE";productionName.text="Queue empty";
                productionProgress.text="Choose a Train or Research action";
                productionFill.rectTransform.sizeDelta=new Vector2(0,12);tint=QueueIdleTint;
            }
            else
            {
                var current=queue[0];
                float progress=current.total>0?Mathf.Clamp01(1-current.remaining/(float)current.total):1;
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
            productionAccent.color=productionFill.color=productionState.color=tint;
            for(int i=0;i<4;i++)
            {
                bool occupied=i+1<queue.Length;
                var item=occupied?queue[i+1]:null;
                queuedNames[i].text=occupied?QueueLabel(item.definition):"Empty";
                queuedNames[i].color=occupied?Color.white:QueueIdleTint;
                queuedKinds[i].text=occupied?(item.research?"Research":UnitVisuals.RoleName(Session.Catalog[item.definition].role)):"Available slot";
                queuedCards[i].color=occupied?button:new Color(.035f,.075f,.105f);
            }
        }
    }
}
