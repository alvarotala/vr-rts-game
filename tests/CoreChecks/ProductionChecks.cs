using Frontier.Core;

static class ProductionChecks
{
    public static void Run(Catalog catalog, MapDefinition map, Action<bool,string> check)
    {
        foreach(string faction in new[]{"industrial","biological"})
        {
            var sim=new Simulation(map,catalog,faction);
            sim.Players[0].minerals=5000;sim.Players[0].catalyst=5000;
            var hq=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="hq");
            var supply=Add("supply",1000);var tech=Add("tech",1001);
            string worker=catalog.Role(faction,"worker").id;
            for(int i=0;i<5;i++)check(Train(hq,worker),"queue five identical workers "+faction);
            var original=hq.queue.ToArray();
            check(original.All(q=>q.id>0)&&original.Select(q=>q.id).Distinct().Count()==5,"duplicate units have distinct order IDs "+faction);
            var view=sim.View(0).entities.First(e=>e.id==hq.id).queue;
            check(view.Select(q=>q.id).SequenceEqual(original.Select(q=>q.id)),"player view preserves queue IDs "+faction);
            view[0].id=-100;check(hq.queue[0].id>0,"view cannot mutate queue identity "+faction);
            hq.queue[0].remaining/=2;int progress=hq.queue[0].remaining,minerals=sim.Players[0].minerals;
            check(Cancel(hq,original[2].id)&&hq.queue.SequenceEqual(original.Where(q=>q!=original[2]))&&hq.queue[0].remaining==progress,
                "cancel middle worker preserves FIFO and active progress "+faction);
            check(sim.Players[0].minerals==minerals+catalog[worker].minerals&&sim.Reserved(0)==4,"cancel refunds exactly one worker and frees its supply "+faction);
            check(Cancel(hq,original[0].id)&&hq.queue[0]==original[1]&&hq.queue[0].remaining==hq.queue[0].total,"cancel active worker starts next with its own full duration "+faction);
            minerals=sim.Players[0].minerals;
            check(!Cancel(hq,original[0].id)&&!Cancel(hq,-1)&&!Cancel(hq,int.MaxValue)&&sim.Players[0].minerals==minerals&&hq.queue.Count==3,
                "stale and invalid IDs never cancel another item or refund again "+faction);
            check(!Cancel(tech,hq.queue[0].id)&&hq.queue.Count==3,"queue IDs cannot cancel an item in another producer "+faction);
            check(!sim.Submit(new GameCommand{kind=CommandKind.Cancel,player=1,entities=new[]{hq.id},queueOrderId=hq.queue[0].id}).accepted,"enemy cannot cancel production "+faction);
            sim.Paused=true;check(!Cancel(hq,hq.queue[0].id)&&hq.queue.Count==3,"paused match rejects queue cancellation "+faction);sim.Paused=false;
            int finished=hq.queue[0].id,waiting=hq.queue[1].id;hq.queue[0].remaining=1;sim.Step();
            check(hq.queue.Count==2&&!Cancel(hq,finished),"completed item cannot cancel the new first item "+faction);
            check(Cancel(hq,waiting)&&hq.queue.Count==1&&hq.queue[0]==original[4],"captured ID still targets the right duplicate after queue advances "+faction);
            check(Train(hq,worker)&&hq.queue.Last().id>original.Max(q=>q.id),"new production never reuses a completed order ID "+faction);
            int last=hq.queue.Last().id;check(Cancel(hq,0)&&hq.queue.All(q=>q.id!=last),"legacy Cancel still removes the last item "+faction);
            hq.queue[0].remaining=0;check(Cancel(hq,hq.queue[0].id)&&hq.queue.Count==0,"finished unit awaiting deployment can be cancelled "+faction);
            check(!Cancel(hq,0),"empty queue cancellation is harmless "+faction);
            var enemyHq=sim.Entities.Values.First(e=>e.owner==1&&sim.Type(e).role=="hq");
            sim.Submit(new GameCommand{kind=CommandKind.Train,player=1,entities=new[]{enemyHq.id},definition=catalog.Role(sim.Players[1].faction,"worker").id});
            check(Train(hq,worker)&&hq.queue[0].id==last+1,"own order IDs reveal no gaps from enemy production "+faction);Cancel(hq,0);

            check(Research("attack")&&Train(tech,catalog.Role(faction,"siege").id)&&Research("armor"),"mixed research and unit queue accepted "+faction);
            var mixed=tech.queue.ToArray();int gas=sim.Players[0].catalyst;minerals=sim.Players[0].minerals;
            var siege=catalog.Role(faction,"siege");
            check(Cancel(tech,mixed[1].id)&&tech.queue.SequenceEqual(new[]{mixed[0],mixed[2]})&&sim.Players[0].minerals==minerals+siege.minerals&&sim.Players[0].catalyst==gas+siege.catalyst,
                "specific artillery cancellation refunds minerals and gas and preserves research "+faction);
            gas=sim.Players[0].catalyst;minerals=sim.Players[0].minerals;
            check(Cancel(tech,mixed[0].id)&&sim.Players[0].attackLevel==0&&sim.Players[0].minerals==minerals+catalog.Upgrade("attack").minerals&&sim.Players[0].catalyst==gas+catalog.Upgrade("attack").catalyst&&Research("attack"),
                "cancelled active research refunds fully and can be queued again "+faction);
            supply.complete=false;int buildings=sim.Buildings(0);minerals=sim.Players[0].minerals;
            check(!Cancel(supply,mixed[2].id)&&sim.Buildings(0)==buildings&&sim.Players[0].minerals==minerals,"queue command cannot accidentally cancel a foundation "+faction);

            EntityState Add(string role,int id)
            {
                var type=catalog.Role(faction,role);var e=new EntityState{id=id,owner=0,definition=type.id,health=type.health,position=new Point(25,id==1000?12:18)};
                sim.Entities.Add(id,e);return e;
            }
            bool Train(EntityState e,string definition)=>sim.Submit(new GameCommand{kind=CommandKind.Train,player=0,entities=new[]{e.id},definition=definition}).accepted;
            bool Research(string id)=>sim.Submit(new GameCommand{kind=CommandKind.Research,player=0,entities=new[]{tech.id},definition=id}).accepted;
            bool Cancel(EntityState e,int id)=>sim.Submit(new GameCommand{kind=CommandKind.Cancel,player=0,entities=new[]{e.id},queueOrderId=id}).accepted;
        }
    }
}
