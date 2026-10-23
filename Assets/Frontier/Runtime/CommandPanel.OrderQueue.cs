using System;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const int OrderColumns=6,OrderCardCount=Simulation.UnitOrderLimit+1;
        private readonly Image[] orderCards=new Image[OrderCardCount];
        private readonly Text[] orderNames=new Text[OrderCardCount],orderNumbers=new Text[OrderCardCount];
        private readonly Target[] orderTargets=new Target[OrderCardCount];
        private string boundOrderSignature;

        private void CreateOrderCards()
        {
            float width=(HudInnerWidth-HudPadding*2-HudGap*(OrderColumns-1))/OrderColumns;
            for(int i=0;i<OrderCardCount;i++)
            {
                bool clear=i==Simulation.UnitOrderLimit;
                var card=Section(orderPlanStrip,clear?"Cancel all orders":"Unit order "+(i+1),
                    new Vector2(HudPadding+i%OrderColumns*(width+HudGap),-HudContentTop-i/OrderColumns*76),new Vector2(width,60),button);
                orderCards[i]=card.GetComponent<Image>();
                orderNumbers[i]=Label(card,clear?"×":(i+1).ToString(),new Vector2(12,-10),new Vector2(30,40),18,ConsoleTheme.Muted,TextAnchor.MiddleCenter);
                orderNames[i]=Label(card,clear?"Cancel all":"",new Vector2(48,-6),new Vector2(width-60,48),20,ConsoleTheme.Ink,TextAnchor.MiddleLeft);
                orderNames[i].fontStyle=FontStyle.Bold;
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
                bool clear=i==Simulation.UnitOrderLimit;if(!clear&&i>=orders.Length)continue;
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
                orderNames[i].text=clear?"Cancel all":occupied?PlanOrderLabel(orders[i].kind):"";
                orderNames[i].color=(clear?orders.Length>0:occupied)?ConsoleTheme.Ink:ConsoleTheme.Muted;
                orderNumbers[i].color=occupied?ConsoleTheme.Ink:ConsoleTheme.Muted;
                orderCards[i].color=OrderCardTint(i,orders.Length,representative);
                ApplyTargetHover(orderTargets[i],hovered==orderTargets[i]&&AcceptsGameInput&&!Quest.Placing);
            }
        }
    }
}
