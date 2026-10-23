using Frontier.Core;

static class MatchReportChecks
{
    public static void Run(Catalog catalog,MapDefinition map,Action<bool,string> check)
    {
        foreach(string faction in new[]{"industrial","biological"})
        {
            var sim=new Simulation(map,catalog,faction,faction);
            var hq=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="hq");
            var workers=sim.Entities.Values.Where(e=>e.owner==0&&sim.Type(e).role=="worker").ToArray();
            var workerType=catalog.Role(faction,"worker");
            foreach(var e in sim.Entities.Values)e.order=CommandKind.Hold;
            check(sim.Report==null,"live report stays private "+faction);
            var hold=new GameCommand{player=0,sequence=1,kind=CommandKind.Hold,entities=workers.Select(e=>e.id).ToArray()};
            check(sim.Submit(hold).accepted&&!sim.Submit(hold).accepted,"report fixture accepts one group order and rejects its duplicate "+faction);
            check(!sim.Submit(new GameCommand{player=0,kind=CommandKind.Train,entities=new[]{hq.id},definition="missing"}).accepted,"rejected production costs no action "+faction);
            check(sim.Submit(new GameCommand{player=0,sequence=2,kind=CommandKind.Stop,entities=new[]{workers[0].id},append=true}).accepted,"queued unit action is accepted "+faction);
            sim.Paused=true;sim.Step();
            check(sim.Tick==0&&!sim.Submit(new GameCommand{player=1,kind=CommandKind.Hold,entities=new[]{hq.id}}).accepted,"pause adds no time or actions "+faction);sim.Paused=false;
            sim.Step(); // Execute a queued order without counting it a second time.
            check(Train(0,hq.id),"report records actual training "+faction);hq.queue[0].remaining=1;sim.Step();
            check(Train(0,hq.id),"report records a cancellable queue entry "+faction);
            check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Cancel,entities=new[]{hq.id}}).accepted,"cancel returns production cost "+faction);
            var enemyHq=sim.Entities.Values.First(e=>e.owner==1&&sim.Type(e).role=="hq");
            check(Train(1,enemyHq.id),"opponent command uses the same report path "+faction);
            // Exact delivered income; cargo lost outside a base must never count.
            foreach(var e in sim.Entities.Values)e.order=CommandKind.Hold;
            var courier=workers[1];courier.position=sim.Center(hq);courier.order=CommandKind.Gather;courier.depositing=true;courier.cargo=8;courier.cargoKind="mineral";
            sim.Step();courier.order=CommandKind.Hold;
            courier.order=CommandKind.Gather;courier.depositing=true;courier.cargo=4;courier.cargoKind="catalyst";sim.Step();courier.order=CommandKind.Hold;
            workers[2].cargo=8;workers[2].health=0;sim.Step();
            while(sim.Tick<Simulation.TickRate*65)sim.Step();
            check(sim.Report==null,"hidden enemy chronology remains unavailable until the finish "+faction);
            check(sim.Submit(new GameCommand{player=1,kind=CommandKind.Surrender}).accepted,"surrender produces a report immediately "+faction);
            var report=sim.Report;var own=report.Players[0];
            check(report.Tick==sim.Tick&&report.Winner==0&&report.Samples[0].Tick==0&&report.Samples[^1].Tick==sim.Tick,"report includes initial and exact final snapshots "+faction);
            check(report.Samples.Zip(report.Samples.Skip(1)).All(pair=>pair.First.Tick<pair.Second.Tick),"sample timestamps are unique and chronological "+faction);
            check(own.Actions==5&&report.Players[1].Actions==1&&own.PeakActionsPerSecond==5,"group, rejection, queued execution and surrender have comparable APM semantics "+faction);
            check(Math.Abs(own.AverageApm-5*60f/65)<.001f&&own.RecentApm==0,"APM uses match time and rolling actions expire "+faction);
            check(own.WorkersProduced==1&&own.WorkersLost==1&&own.PeakWorkers==7&&own.Workers==6,"workers track production, death and peak separately from starting workers "+faction);
            check(own.MineralsCollected==8&&own.CatalystCollected==4,"only delivered cargo counts as collected income "+faction);
            check(own.MineralsSpent==workerType.minerals*2&&own.MineralsRefunded==workerType.minerals&&own.CatalystSpent==0,"gross spending and refunds are separate "+faction);
            check(own.Minerals==400+8-own.MineralsSpent+own.MineralsRefunded&&own.Catalyst==4,"resource accounting reconciles with the final bank "+faction);
            check(report.BuildOrders[0].Count==2&&report.BuildOrders[0][0].Status==BuildOrderStatus.Completed&&report.BuildOrders[0][1].Status==BuildOrderStatus.Cancelled,"build order preserves completed and cancelled production "+faction);
            check(report.BuildOrders[1].Count==1&&report.Players[1].WorkersProduced==1,"opponent chronology and totals unlock on finish "+faction);
            int tick=sim.Tick;sim.Step();sim.Players[0].minerals=9999;
            check(sim.Tick==tick&&ReferenceEquals(report,sim.Report)&&report.Players[0].Minerals==own.Minerals,"finished report is a stable detached snapshot "+faction);
            var fresh=new Simulation(map,catalog,faction,faction);fresh.Submit(new GameCommand{player=0,kind=CommandKind.Surrender});
            check(fresh.Report.Players.All(p=>p.Actions==0&&p.AverageApm==0)&&fresh.Report.Samples.Count==1&&fresh.Report.BuildOrders.All(o=>o.Count==0),"instant rematch result resets statistics and has finite zero APM "+faction);
            bool Train(int p,int producer)=>sim.Submit(new GameCommand{player=p,kind=CommandKind.Train,entities=new[]{producer},definition=workerType.id}).accepted;
        }
        ConstructionAndLosses(catalog,map,check);
    }

    private static void ConstructionAndLosses(Catalog catalog,MapDefinition map,Action<bool,string> check)
    {
        var sim=new Simulation(map,catalog);sim.Players[0].minerals=10000;sim.Players[0].catalyst=10000;
        var worker=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="worker");
        EntityState Build(string role)
        {
            string definition=catalog.Role("industrial",role).id;
            for(int y=1;y<40;y++)for(int x=1;x<40;x++)
                if(sim.Submit(new GameCommand{player=0,kind=CommandKind.Build,entities=new[]{worker.id},definition=definition,position=new Point(x,y)}).accepted)
                    return sim.Entities.Values.Where(e=>e.owner==0&&e.definition==definition).OrderByDescending(e=>e.id).First();
            throw new Exception("No visible construction site for report fixture");
        }
        var cancelled=Build("supply");sim.Submit(new GameCommand{player=0,kind=CommandKind.Cancel,entities=new[]{cancelled.id}});
        var supply=Build("supply");worker.position=sim.Center(supply);supply.remaining=1;sim.Step();
        check(supply.complete,"report fixture completes a real foundation");
        var barracks=Build("barracks");worker.position=sim.Center(barracks);barracks.remaining=1;sim.Step();
        var tech=Build("tech");worker.position=sim.Center(tech);tech.remaining=1;sim.Step();
        check(tech.complete,"report fixture completes technology structure");
        var lab=Build("research");worker.position=sim.Center(lab);lab.remaining=1;sim.Step();
        check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Research,entities=new[]{lab.id},definition="attack"}).accepted,"research enters build order");
        lab.queue[0].remaining=1;sim.Step();
        string siege=catalog.Role("industrial","siege").id;
        check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Train,entities=new[]{tech.id},definition=siege}).accepted,"army production enters build order");
        tech.queue[0].remaining=1;sim.Step();
        var soldier=sim.Entities.Values.First(e=>e.owner==0&&e.definition==siege);soldier.health=0;
        check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Train,entities=new[]{tech.id},definition=siege}).accepted,"producer has a queued casualty");
        tech.health=0;sim.Step();
        var unfinished=Build("supply");
        foreach(var e in sim.Entities.Values.Where(e=>e.owner==1&&sim.Type(e).building))e.health=0;
        sim.Step();var r=sim.Report;var p=r.Players[0];
        check(r!=null&&r.Winner==0&&r.Tick==sim.Tick,"destroying the last enemy structure finalizes a report");
        check(p.StructuresCompleted==4&&p.StructuresLost==1&&p.ArmyProduced==1&&p.ArmyLost==1&&p.PeakArmy==1,"completed structures, army and losses are counted at actual events");
        check(r.BuildOrders[0].Count==9&&r.BuildOrders[0][0].Status==BuildOrderStatus.Cancelled&&r.BuildOrders[0][5].Kind==CommandKind.Research&&r.BuildOrders[0][5].Status==BuildOrderStatus.Completed,"construction, cancellation and research retain chronological entries");
        check(r.BuildOrders[0][7].Status==BuildOrderStatus.Destroyed&&r.BuildOrders[0][8].Status==BuildOrderStatus.Pending,"destroyed producer and unfinished construction have distinct outcomes");
        check(r.BuildOrders[0].All(e=>e.EndTick<0||e.EndTick>=e.Tick),"completion timestamps never precede orders");
    }
}
