using Frontier.Core;

static class HarvestingChecks
{
    public static void Depletion(Catalog catalog, IEnumerable<MapDefinition> maps, Action<bool,string> check)
    {
        void Step(Simulation sim,int count) {for(int i=0;i<count;i++)sim.Step();}
        foreach(var map in maps)
        foreach(string faction in new[]{"industrial","biological"})
        foreach(bool explored in new[]{false,true})
        {
            var sim=new Simulation(map,catalog,faction);
            var workers=sim.Entities.Values.Where(e=>e.owner==0&&sim.Type(e).role=="worker").ToArray();
            foreach(var enemy in sim.Entities.Values.Where(e=>e.owner==1&&!sim.Type(e).building))enemy.order=CommandKind.Hold;
            var local=sim.Resources.Where(r=>r.kind=="mineral"&&r.definition.StartsWith("camp0.")).ToArray();
            var remote=sim.Resources.First(r=>r.definition==(map.id=="rift"?"camp2.mineral0":"camp1.mineral0"));
            string label=map.id+"/"+faction+"/"+(explored?"remembered":"unexplored");
            if(explored)
            {
                var type=catalog.Role(faction,"supply");
                var observer=new EntityState{id=1000,owner=0,definition=type.id,health=type.health,
                    position=new Point(remote.position.x-type.footprint*.5f,remote.position.y-type.footprint*.5f)};
                sim.Entities.Add(observer.id,observer);Step(sim,4);sim.Entities.Remove(observer.id);Step(sim,4);
            }
            check(sim.Explored(0,remote.position)==explored&&!sim.Visible(0,remote.position),"Depletion fixture preserves remote fog: "+label);
            foreach(var mineral in local)mineral.remaining=0;
            foreach(var worker in workers){worker.cargo=0;worker.depositing=false;}
            // The last partial load is still owed to the player's economy.
            workers[0].cargo=5;workers[0].cargoKind="mineral";
            int money=sim.Players[0].minerals;
            bool stayedLocal=true;
            for(int i=0;i<600;i++)
            {
                sim.Step();
                stayedLocal&=workers.All(w=>w.order!=CommandKind.Gather||local.Any(r=>r.id==w.resourceId));
            }
            check(stayedLocal&&workers.All(w=>w.order==CommandKind.Stop&&w.resourceId==0),
                "Exhausted camp leaves all miners idle without transferring to other zones: "+label);
            check(sim.Players[0].minerals==money+5&&workers.All(w=>w.cargo==0),"Last partial mineral load is deposited before idling: "+label);
            var positions=workers.Select(w=>w.position).ToArray();Step(sim,200);
            check(workers.Select((w,i)=>w.order==CommandKind.Stop&&w.position.Distance(positions[i])<.01f).All(x=>x),
                "Idle miners stay at the exhausted camp: "+label);
            var hq=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="hq");
            check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Train,entities=new[]{hq.id},definition=catalog.Role(faction,"worker").id}).accepted,
                "Worker can still be trained at an exhausted camp: "+label);
            Step(sim,600);
            var born=sim.Entities.Values.Where(e=>e.owner==0&&sim.Type(e).role=="worker"&&!workers.Contains(e)).ToArray();
            check(born.Length==1&&born[0].order==CommandKind.Stop&&born[0].resourceId==0,
                "New worker does not auto-transfer from an exhausted camp: "+label);
            if(explored)
                check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Gather,entities=new[]{workers[0].id},target=remote.id}).accepted
                    &&workers[0].order==CommandKind.Gather&&workers[0].resourceId==remote.id,
                    "Explicit transfer to remembered remote minerals remains available: "+label);
        }
    }

    public static void Run(Catalog catalog, MapDefinition map, Action<bool,string> check)
    {
        EntityState[] Workers(Simulation s) => s.Entities.Values.Where(e=>e.owner==0&&s.Type(e).role=="worker").OrderBy(e=>e.id).ToArray();
        ResourceState[] Minerals(Simulation s) => s.Resources.Where(r=>r.kind=="mineral"&&r.position.Distance(new Point(14,20))<8).ToArray();
        bool Assign(Simulation s,IEnumerable<EntityState> workers,int resource) => s.Submit(new GameCommand{kind=CommandKind.Gather,player=0,entities=workers.Select(w=>w.id).ToArray(),target=resource}).accepted;
        void Step(Simulation s,int count) {for(int i=0;i<count;i++)s.Step();}
        void Stop(Simulation s,IEnumerable<EntityState> workers) => s.Submit(new GameCommand{kind=CommandKind.Stop,player=0,entities=workers.Select(w=>w.id).ToArray()});
        EntityState Extractor(Simulation s,Point position,int id)
        {
            var type=catalog.Role(s.Players[0].faction,"extractor");
            var building=new EntityState{id=id,owner=0,definition=type.id,position=position,health=type.health};
            s.Entities.Add(id,building);s.Map.Occupy(position,type.footprint,id);return building;
        }
        foreach(string faction in new[]{"industrial","biological"})
        {
            var s=new Simulation(map,catalog,faction);var workers=Workers(s);var minerals=Minerals(s);
            check(workers.All(w=>w.order==CommandKind.Gather)&&workers.GroupBy(w=>w.resourceId).All(g=>g.Count()<=2),"initial workers respect mineral optimum "+faction);
            check(Assign(s,workers,minerals[0].id),"group gather accepted "+faction);
            check(workers.Count(w=>w.resourceId==minerals[0].id)==2&&workers.GroupBy(w=>w.resourceId).All(g=>g.Count()<=2),"group reserves two places per mineral before assigning the next worker "+faction);
            var firstAssignments=workers.Select(w=>w.resourceId).ToArray();
            Assign(s,workers,minerals[0].id);
            check(workers.Select(w=>w.resourceId).SequenceEqual(firstAssignments),"repeated group order does not double-count reservations "+faction);
            var incoming=workers.First(w=>w.resourceId!=minerals[0].id);
            workers[0].cargo=8;workers[0].cargoKind="mineral";workers[0].depositing=true;
            Assign(s,new[]{incoming},minerals[0].id);
            check(incoming.resourceId!=minerals[0].id&&s.View(0).resources.First(r=>r.id==minerals[0].id).assignedWorkers==2,"delivery retains its reserved slot "+faction);
            Stop(s,new[]{workers[0]});Assign(s,new[]{incoming},minerals[0].id);
            check(incoming.resourceId==minerals[0].id,"stopping releases a slot immediately "+faction);
            var hq=s.Entities.Values.First(e=>e.owner==0&&s.Type(e).role=="hq");
            var known=s.View(0).resources.Where(r=>r.baseId==hq.id&&r.kind=="mineral").ToArray();
            check(known.Sum(r=>r.optimalWorkers)==8&&known.Sum(r=>r.assignedWorkers)==5,"base mineral counter sums its deposits and own active assignments "+faction);
            var depleted=s.Resources.First(r=>r.id==incoming.resourceId);depleted.remaining=0;incoming.cargo=0;incoming.depositing=false;
            Step(s,5);
            check(incoming.order==CommandKind.Gather&&incoming.resourceId!=depleted.id&&s.View(0).resources.First(r=>r.id==depleted.id).optimalWorkers==0,"depletion redirects worker and removes exhausted capacity "+faction);

            s=new Simulation(map,catalog,faction);workers=Workers(s);Stop(s,workers);
            var gas=s.Resources.First(r=>r.kind=="catalyst"&&r.position.x<30);
            check(!Assign(s,workers,gas.id),"gas without an extractor rejects "+faction);
            float half=catalog.Role(faction,"extractor").footprint*.5f;
            var first=Extractor(s,new Point(gas.position.x-half,gas.position.y-half),1000);
            var secondGas=new ResourceState{id=100,definition="test.gas",kind="catalyst",position=new Point(gas.position.x,gas.position.y+8),remaining=2000};s.Resources.Add(secondGas);
            var second=Extractor(s,new Point(secondGas.position.x-half,secondGas.position.y-half),1001);
            workers[0].position=new Point(secondGas.position.x,secondGas.position.y-4);Step(s,4);
            check(Assign(s,workers,gas.id)&&workers.Count(w=>w.resourceId==gas.id)==3&&workers.Count(w=>w.resourceId==secondGas.id)==3,"gas group splits three per operating extractor "+faction);
            var projected=s.View(0).resources.First(r=>r.id==gas.id);
            check(projected.assignedWorkers==3&&projected.optimalWorkers==3&&projected.extractorId==first.id,"extractor exposes three-worker optimum "+faction);
            var gasWorker=workers[0];gasWorker.cargo=5;gasWorker.cargoKind="catalyst";
            int balance=s.Players[0].catalyst;
            Assign(s,new[]{gasWorker},Minerals(s)[0].id);Step(s,450);
            check(s.Players[0].catalyst>=balance+5&&gasWorker.cargoKind=="mineral","switching resource delivers old cargo without converting it "+faction);
            second.health=0;Step(s,400);
            check(workers.All(w=>w.resourceId!=secondGas.id||w.order!=CommandKind.Gather),"destroyed extractor redirects assigned workers "+faction);

            s=new Simulation(map,catalog,faction);workers=Workers(s);minerals=Minerals(s);Stop(s,workers);
            foreach(var r in minerals.Skip(1))r.remaining=0;
            Assign(s,workers,minerals[0].id);
            check(s.View(0).resources.First(r=>r.id==minerals[0].id).assignedWorkers==6,"full mineral line keeps excess workers visibly assigned "+faction);
            minerals[1].remaining=1800;workers[0].cargo=8;workers[0].cargoKind="mineral";workers[0].depositing=true;
            workers[0].position=new Point(11.5f,14.5f);Step(s,1);
            check(workers[0].resourceId==minerals[1].id,"overflow moves to a newly free deposit after delivering "+faction);

            // Move an existing miner to a new camp and build through the real command path.
            // The old resource assignment must not send the constructor back across the map.
            s=new Simulation(map,catalog,faction);workers=Workers(s);Stop(s,workers);
            var builder=workers[0];int oldResource=builder.resourceId;
            check(s.Submit(new GameCommand{player=0,kind=CommandKind.Move,entities=new[]{builder.id},position=new Point(27.5f,45.5f)}).accepted,
                "expansion builder can travel to the new camp "+faction);
            Step(s,600);
            check(s.Submit(new GameCommand{player=0,kind=CommandKind.Build,entities=new[]{builder.id},definition=catalog.Role(faction,"hq").id,position=new Point(30,40)}).accepted,
                "expansion foundation accepted "+faction);
            var expansion=s.Entities.Values.Last(e=>e.owner==0&&s.Type(e).role=="hq");
            for(int i=0;i<1000&&builder.order==CommandKind.Build;i++)s.Step();
            var localResource=s.Resources.First(r=>r.id==builder.resourceId);
            check(builder.order==CommandKind.Gather&&builder.resourceId!=oldResource&&localResource.position.Distance(s.Center(expansion))<Harvesting.SearchRadius,
                "expansion constructor resumes gathering at its new camp "+faction);
            Stop(s,new[]{builder});Step(s,800);
            check(expansion.complete,"expansion finishes before receiving resources "+faction);
            check(Assign(s,new[]{builder},localResource.id),"expansion minerals accept gather order "+faction);
            int delivered=0;bool localDeliveries=true;int income=s.Players[0].minerals;
            for(int i=0;i<1200&&delivered<3;i++)
            {
                int cargo=builder.cargo;s.Step();
                if(cargo>0&&builder.cargo==0)
                {delivered++;localDeliveries&=builder.position.Distance(s.Center(expansion))<=s.Type(expansion).footprint*.75f+1.01f;}
            }
            check(delivered==3&&localDeliveries&&s.Players[0].minerals==income+24,
                "nearby completed expansion receives three full mineral deliveries "+faction);
        }
        var sim=new Simulation(map,catalog);var units=Workers(sim);Stop(sim,units);var nodes=Minerals(sim);
        for(int y=0;y<sim.Map.Height;y++)sim.Map.Walkable[sim.Map.Index(15,y)]=false;
        check(Assign(sim,new[]{units[0]},nodes.Last().id)&&units[0].resourceId!=nodes.Last().id,"unreachable preferred patch falls back to a reachable neighbor");
        for(int y=0;y<sim.Map.Height;y++)sim.Map.Walkable[sim.Map.Index(15,y)]=true;
        var unseen=sim.Resources.First(r=>r.definition=="camp2.mineral0");
        check(!Assign(sim,new[]{units[0]},unseen.id),"unexplored resources cannot be assigned");
        units[0].position=new Point(unseen.position.x,unseen.position.y-2);Step(sim,4);
        units[0].position=new Point(12,18);Step(sim,4);int remembered=sim.View(0).resources.First(r=>r.id==unseen.id).remaining;
        unseen.remaining=0;
        var enemy=sim.Entities.Values.First(e=>e.owner==1&&sim.Type(e).role=="worker");enemy.order=CommandKind.Gather;enemy.resourceId=enemy.target=unseen.id;
        check(!sim.Visible(0,unseen.position)&&sim.View(0).resources.First(r=>r.id==unseen.id).remaining==remembered&&sim.View(0).resources.First(r=>r.id==unseen.id).assignedWorkers==0,"resource counters do not reveal hidden depletion or enemy workers");
        check(Assign(sim,new[]{units[0]},unseen.id)&&units[0].resourceId==unseen.id,"allocation uses remembered resource state under fog");
    }
}
