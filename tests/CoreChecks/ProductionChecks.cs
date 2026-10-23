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
            var supply=Add("supply",1000);var tech=Add("tech",1001);var lab=Add("research",1002);
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

            check(Research("attack")&&Train(tech,catalog.Role(faction,"siege").id)&&Research("armor"),"independent research and unit queues accepted "+faction);
            var mixed=lab.queue.ToArray();int gas=sim.Players[0].catalyst;minerals=sim.Players[0].minerals;
            var siege=catalog.Role(faction,"siege");
            check(Cancel(tech,tech.queue[0].id)&&tech.queue.Count==0&&lab.queue.SequenceEqual(mixed)&&sim.Players[0].minerals==minerals+siege.minerals&&sim.Players[0].catalyst==gas+siege.catalyst,
                "specific artillery cancellation refunds minerals and gas and preserves research "+faction);
            gas=sim.Players[0].catalyst;minerals=sim.Players[0].minerals;
            check(Cancel(lab,mixed[0].id)&&sim.Players[0].attackLevel==0&&sim.Players[0].minerals==minerals+catalog.Upgrade("attack").minerals&&sim.Players[0].catalyst==gas+catalog.Upgrade("attack").catalyst&&Research("attack"),
                "cancelled active research refunds fully and can be queued again "+faction);
            supply.complete=false;int buildings=sim.Buildings(0);minerals=sim.Players[0].minerals;
            check(!Cancel(supply,mixed[1].id)&&sim.Buildings(0)==buildings&&sim.Players[0].minerals==minerals,"queue command cannot accidentally cancel a foundation "+faction);
            check(!CancelConstruction(supply,tech.definition)&&sim.Entities.ContainsKey(supply.id),"construction cancellation validates the captured building type "+faction);
            check(CancelConstruction(supply,supply.definition)&&!sim.Entities.ContainsKey(supply.id)
                &&sim.Players[0].minerals==minerals+(int)(catalog[supply.definition].minerals*.75f),"construction card cancels the foundation and refunds 75 percent "+faction);
            var foundation=Add("research",1003);foundation.complete=false;minerals=sim.Players[0].minerals;gas=sim.Players[0].catalyst;
            check(CancelConstruction(foundation,foundation.definition)&&sim.Players[0].minerals==minerals+(int)(catalog[foundation.definition].minerals*.75f)
                &&sim.Players[0].catalyst==gas+(int)(catalog[foundation.definition].catalyst*.75f),"construction refund includes the original gas cost "+faction);
            check(Train(hq,worker),"finished building starts production before a delayed construction cancellation "+faction);
            minerals=sim.Players[0].minerals;int newOrder=hq.queue[0].id;
            check(!CancelConstruction(hq,hq.definition)&&hq.queue.Count==1&&hq.queue[0].id==newOrder&&sim.Players[0].minerals==minerals,
                "delayed construction cancellation never cancels a finished building's new production "+faction);

            // Exercise the actual cap, including reservations and deployment.
            sim=new Simulation(map,catalog,faction);sim.Players[0].minerals=5000;
            hq=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="hq");
            var supplyType=catalog.Role(faction,"supply");
            for(int i=0;i<6;i++)sim.Entities.Add(1000+i,new EntityState{id=1000+i,owner=0,definition=supplyType.id,health=supplyType.health,position=new Point(25+i*3,12)});
            for(int i=sim.Units(0);i<Simulation.UnitLimit-2;i++)
                sim.Entities.Add(2000+i,new EntityState{id=2000+i,owner=0,definition=worker,health=catalog[worker].health,position=new Point(24.5f+i%10,22.5f+i/10)});
            check(sim.Capacity(0)==Simulation.UnitLimit&&sim.View(0).capacity==Simulation.UnitLimit,"excess supply buildings and HUD projection respect unit limit "+faction);
            check(Train(hq,worker)&&Train(hq,worker)&&sim.Units(0)+sim.Reserved(0)==Simulation.UnitLimit,"production reserves the final two population slots "+faction);
            minerals=sim.Players[0].minerals;
            check(!Train(hq,worker)&&sim.Players[0].minerals==minerals&&sim.Reserved(0)==2,"queued production cannot exceed the cap or spend resources "+faction);
            check(Cancel(hq,hq.queue[0].id)&&Train(hq,worker),"cancelling at the cap releases one slot "+faction);
            foreach(var order in hq.queue)order.remaining=1;
            sim.Step();sim.Step();
            check(sim.Units(0)==Simulation.UnitLimit&&sim.Reserved(0)==0&&!Train(hq,worker),"last units deploy and the next unit is rejected "+faction);
            var army=sim.Entities.Values.Where(e=>e.owner==0&&!sim.Type(e).building).Select(e=>e.id).ToArray();
            check(sim.Submit(new GameCommand{kind=CommandKind.Hold,player=0,entities=army}).accepted,"one command accepts the entire maximum army "+faction);
            check(sim.Capacity(1)==10&&sim.Units(1)==6,"population limit applies independently to each player "+faction);

            EntityState Add(string role,int id)
            {
                var type=catalog.Role(faction,role);var e=new EntityState{id=id,owner=0,definition=type.id,health=type.health,position=new Point(25,12+(id-1000)*6)};
                sim.Entities.Add(id,e);return e;
            }
            bool Train(EntityState e,string definition)=>sim.Submit(new GameCommand{kind=CommandKind.Train,player=0,entities=new[]{e.id},definition=definition}).accepted;
            bool Research(string id)=>sim.Submit(new GameCommand{kind=CommandKind.Research,player=0,entities=new[]{lab.id},definition=id}).accepted;
            bool Cancel(EntityState e,int id)=>sim.Submit(new GameCommand{kind=CommandKind.Cancel,player=0,entities=new[]{e.id},queueOrderId=id}).accepted;
            bool CancelConstruction(EntityState e,string definition)=>sim.Submit(new GameCommand{kind=CommandKind.Cancel,player=0,entities=new[]{e.id},definition=definition}).accepted;
        }
    }
}
