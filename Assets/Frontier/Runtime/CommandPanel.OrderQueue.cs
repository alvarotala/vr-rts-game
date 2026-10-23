using System;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const int OrderColumns=5,OrderCardCount=Simulation.UnitOrderLimit+1;
        private readonly Image[] orderCards=new Image[OrderCardCount];
        private readonly Text[] orderNames=new Text[OrderCardCount],orderNumbers=new Text[OrderCardCount];
        private readonly Target[] orderTargets=new Target[OrderCardCount];
        private readonly Text[] orderArrows=new Text[Simulation.UnitOrderLimit-1];
        private string boundOrderSignature;

        private void CreateOrderCards()
        {
            const float currentWidth=300,arrowGap=24;
            float width=(HudInnerWidth-HudPadding*2-currentWidth-arrowGap*OrderColumns)/OrderColumns;
            for(int i=0;i<OrderCardCount;i++)
            {
                bool clear=i==Simulation.UnitOrderLimit,current=i==0;
                int slot=i-1;
                var position=clear?new Vector2(HudInnerWidth-HudPadding-44-HudGap-200,-16):current?new Vector2(HudPadding,-HudContentTop):
                    new Vector2(HudPadding+currentWidth+arrowGap+slot%OrderColumns*(width+arrowGap),-HudContentTop-slot/OrderColumns*76);
                var size=clear?new Vector2(200,44):current?new Vector2(currentWidth,136):new Vector2(width,60);
                var card=Section(orderPlanStrip,clear?"Cancel all orders":"Unit order "+(i+1),position,size,button);
                orderCards[i]=card.GetComponent<Image>();
                orderNumbers[i]=Label(card,clear?"×":current?"1 · CURRENT":(i+1).ToString(),
                    current?new Vector2(24,-12):new Vector2(8,-6),current?new Vector2(currentWidth-48,24):new Vector2(24,size.y-12),
                    current?18:16,ConsoleTheme.Muted,TextAnchor.MiddleLeft);
                orderNames[i]=Label(card,clear?"Cancel all":"",current?new Vector2(24,-44):new Vector2(40,-6),
                    current?new Vector2(currentWidth-48,72):new Vector2(size.x-52,size.y-12),current?26:20,ConsoleTheme.Ink,TextAnchor.MiddleLeft);
                orderNames[i].fontStyle=FontStyle.Bold;
                if(!current&&!clear)
                {
                    orderArrows[slot]=Label(orderPlanStrip,slot==OrderColumns?"↳":"→",position+new Vector2(-arrowGap,-16),
                        new Vector2(arrowGap,28),22,ConsoleTheme.Muted,TextAnchor.MiddleCenter);
                    orderArrows[slot].name="Order continuation "+(i+1);
                    orderArrows[slot].gameObject.SetActive(false);
                }
                card.gameObject.SetActive(false);
            }
        }

        private string OrderPlanSignature()=>Page!=MenuPage.Actions?"":string.Join(";",SelectedMobileUnits().Select(e=>
            e.id+":"+e.activeOrderSequence+":"+e.order+":"+e.target+":"+string.Join(",",e.pendingOrders.Select(q=>q.sourceSequence))));

        private UnitOrder[] DisplayedUnitOrders(out VisibleEntity representative)
        {
            var units=SelectedMobileUnits();
            representative=units.FirstOrDefault(e=>e.pendingOrders.Length>0)??units.FirstOrDefault(e=>e.order!=CommandKind.Stop);
            if(representative==null)return Array.Empty<UnitOrder>();
            var current=representative.order==CommandKind.Stop?Array.Empty<UnitOrder>():new[]{new UnitOrder {
                sourceSequence=representative.activeOrderSequence,kind=representative.order,target=representative.target,position=representative.destination }};
            return current.Concat(representative.pendingOrders).Take(Simulation.UnitOrderLimit).ToArray();
        }

        private void ClearOrderTargets()
        {
            for(int i=0;i<orderTargets.Length;i++)
            {
                var target=orderTargets[i];if(target==null)continue;
                ApplyTargetHover(target,false);targets.Remove(target);
                if(pressed==target)pressed=null;
                if(hovered==target)hovered=null;
                orderTargets[i]=null;
            }
            boundOrderSignature=null;
        }

        private void RefreshOrderTargets()
        {
            if(Page!=MenuPage.Actions||boundOrderSignature==OrderPlanSignature())return;
            // Advancing a unit's plan only rebinds these cards, preserving unrelated
            // buttons and pointer gestures elsewhere in the fixed HUD.
            ClearOrderTargets();BindOrderTargets();nextText=0;
        }

        private void BindOrderTargets()
        {
            boundOrderSignature=OrderPlanSignature();
            var orders=DisplayedUnitOrders(out var representative);if(orders.Length==0)return;
            var match=Session.SessionIdentity;var signatureAtBind=OrderPlanSignature();
            var selected=Session.Selection.OrderBy(id=>id).ToArray();
            for(int i=0;i<OrderCardCount;i++)
            {
                bool clear=i==Simulation.UnitOrderLimit;if(clear?orders.Length<=1:i>=orders.Length)continue;
                var order=clear?null:orders[i];
                // Sequence IDs identify the same command across a formation. Automatic
                // orders have sequence zero, so only address the displayed unit for those.
                int[] ids=SelectedMobileUnits().Where(e=>clear||(order.sourceSequence==0?e.id==representative.id:
                    e.order!=CommandKind.Stop&&e.activeOrderSequence==order.sourceSequence||e.pendingOrders.Any(q=>q.sourceSequence==order.sourceSequence)))
                    .Select(e=>e.id).ToArray();
                var target=new Target{rect=orderCards[i].rectTransform,image=orderCards[i],normal=OrderCardTint(i,orders.Length,representative),click=()=>
                {
                    if(!AcceptsGameInput||Session.SessionIdentity!=match||OrderPlanSignature()!=signatureAtBind
                        ||!Session.Selection.OrderBy(id=>id).SequenceEqual(selected))return;
                    if(Session.Send(clear?CommandKind.Stop:CommandKind.CancelUnitOrder,unitOrderSequence:clear?0:order.sourceSequence,entityIds:ids))
                        Session.Announce(Session.IsOnline?"Cancellation requested.":clear?"Current and queued orders cancelled.":"Unit order cancelled.");
                    nextText=0;
                }};
                orderTargets[i]=target;targets.Add(target);
            }
        }

        private Color OrderCardTint(int index,int count,VisibleEntity representative)=>index==Simulation.UnitOrderLimit&&count>0?ConsoleTheme.Danger:
            index==0&&representative!=null&&representative.order!=CommandKind.Stop?QueueActiveTint:button;

        private void UpdateOrderPlanHud()
        {
            var orders=DisplayedUnitOrders(out var representative);
            orderPlanCount.text=orders.Length+" / "+Simulation.UnitOrderLimit;
            orderPlanHint.text="Up to 11 orders including the current one. Click an order to remove it; cancelling the current one starts the next. Cancel all stops the selected units and clears their pending orders.";
            if(SelectedMobileUnits().Length>1)((LocalizedText)orderPlanHint).Append("\n\nThe grid shows one selected unit's plan. Removing a shared order removes that same command from the selected units, preserving their other orders.");
            for(int i=0;i<OrderCardCount;i++)
            {
                bool clear=i==Simulation.UnitOrderLimit,occupied=i<orders.Length;
                orderCards[i].gameObject.SetActive(clear?orders.Length>1:occupied);
                if(i>0&&!clear)orderArrows[i-1].gameObject.SetActive(occupied);
                orderNames[i].text=clear?"Cancel all":occupied?PlanOrderLabel(orders[i].kind):"";
                orderNames[i].color=(clear?orders.Length>0:occupied)?ConsoleTheme.Ink:ConsoleTheme.Muted;
                orderNumbers[i].color=occupied?ConsoleTheme.Ink:ConsoleTheme.Muted;
                orderCards[i].color=OrderCardTint(i,orders.Length,representative);
                ApplyTargetHover(orderTargets[i],hovered==orderTargets[i]&&AcceptsGameInput&&!Quest.Placing);
            }
        }
    }
}
