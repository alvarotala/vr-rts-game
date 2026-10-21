using Frontier.Core;

static class ResourceLayoutChecks
{
    public static void Run(Catalog catalog, IEnumerable<MapDefinition> maps, Action<bool,string> check)
    {
        foreach(var map in maps)
        {
            var camps=map.resources.GroupBy(r=>r.id.Split('.')[0]).ToArray();
            check(map.id=="training"?map.title=="Neon Loop"&&map.theme=="neon"&&camps.Length==4:
                map.title=="Forest Watch"&&map.theme=="forest"&&camps.Length==6,
                "Map identity, theme and resource camps match the selected battlefield: "+map.id);
            foreach(var spawn in map.spawns)
            {
                var center=new Point(spawn.x+2,spawn.y+2);
                var deposits=map.resources.Where(r=>r.id.StartsWith("camp"+spawn.player+".")).ToArray();
                var minerals=deposits.Where(r=>r.kind=="mineral").ToArray();var gas=deposits.Single(r=>r.kind=="catalyst");
                Point At(ResourceDefinition r)=>new Point(r.x+.5f,r.y+.5f);
                var radii=deposits.Select(r=>At(r).Distance(center)).ToArray();
                check(minerals.Length==4&&minerals.All(r=>r.amount==1800)&&gas.amount==2400,"Starting camp preserves resource quantities: "+map.id+"/"+spawn.player);
                check(radii.Max()-radii.Min()<.5f&&radii.Min()>6&&radii.Max()<7,"Minerals and gas share a concentric band around HQ: "+map.id+"/"+spawn.player);
                check(Math.Abs(At(gas).y-center.y)<1&&Math.Abs(At(gas).x-center.x)>6,"Gas sits beside headquarters: "+map.id+"/"+spawn.player);
                check(Math.Abs(minerals.Average(r=>At(r).x)-center.x)<.001f&&minerals.Select(r=>r.y).Distinct().Count()>1,"Mineral arc is centered and curved: "+map.id+"/"+spawn.player);
            }
            check(map.resources.All(a=>map.resources.Any(b=>b.kind==a.kind&&b.amount==a.amount&&b.x==map.width-1-a.x&&b.y==map.height-1-a.y)),"Every camp retains rotational symmetry: "+map.id);
            if(map.id=="rift")
            {
                var forest=new GridMap(map);
                check(Enumerable.Range(0,forest.Count).All(i=>forest.Walkable[i]==forest.Walkable[forest.Count-1-i]
                    &&forest.Buildable[i]==forest.Buildable[forest.Count-1-i]),"Forest Watch groves preserve rotational terrain symmetry");
                foreach(var site in new[]{new Point(30,40),new Point(94,84),new Point(30,91),new Point(94,33)})
                    check(forest.CanPlace(site,4)&&!map.resources.Any(r=>Inside(r,site,4)),
                        "Forest Watch grove placement preserves expansion headquarters pad: "+site.x+","+site.y);
                foreach(var gas in map.resources.Where(r=>r.kind=="catalyst"))
                    check(forest.CanPlace(new Point(gas.x-1,gas.y-1),3),"Forest Watch grove placement preserves extractor pad: "+gas.id);
            }
            foreach(string faction in new[]{"industrial","biological"})
            {
                var sim=new Simulation(map,catalog,faction,faction);var extractor=catalog.Role(faction,"extractor");
                var orders=new List<(int player,int worker,int gas)>();
                for(int player=0;player<2;player++)
                {
                    var worker=sim.Entities.Values.First(e=>e.owner==player&&sim.Type(e).role=="worker");
                    var gas=sim.Resources.Single(r=>r.definition=="camp"+player+".catalyst");
                    var position=new Point(gas.position.x-extractor.footprint*.5f,gas.position.y-extractor.footprint*.5f);
                    check(sim.Submit(new GameCommand{player=player,kind=CommandKind.Build,entities=new[]{worker.id},definition=extractor.id,position=position}).accepted,"Lateral gas accepts an extractor: "+map.id+"/"+faction+"/"+player);
                    orders.Add((player,worker.id,gas.id));
                }
                for(int tick=0;tick<1000;tick++)sim.Step();
                check(sim.Entities.Values.Count(e=>e.definition==extractor.id&&e.complete)==2,"Workers reach and finish both lateral extractors: "+map.id+"/"+faction);
                foreach(var order in orders)check(sim.Submit(new GameCommand{player=order.player,kind=CommandKind.Gather,entities=new[]{order.worker},target=order.gas}).accepted,"Lateral gas accepts harvesting: "+map.id+"/"+faction+"/"+order.player);
                for(int tick=0;tick<1000;tick++)sim.Step();
                check(sim.Players.All(p=>p.catalyst>0),"Both sides deliver gas from the new ring: "+map.id+"/"+faction);
            }
            if(map.id=="training")NeonExpansions(catalog,map,check);
        }
    }

    private static void NeonExpansions(Catalog catalog,MapDefinition map,Action<bool,string> check)
    {
        var sites=new[]{new Point(32,8),new Point(12,36)};
        var grid=new GridMap(map);
        check(map.resources.Where(r=>r.kind=="mineral").Any(r=>map.spawns.All(s=>new Point(r.x+.5f,r.y+.5f)
            .Distance(new Point(s.x+2,s.y+2))>Harvesting.BaseRadius+4)),
            "Neon Loop has neutral deposits outside both starting bases' gathering radius for AI expansion");
        for(int i=0;i<sites.Length;i++)
        {
            var deposits=map.resources.Where(r=>r.id.StartsWith("camp"+(i+2)+".")).ToArray();
            check(deposits.Count(r=>r.kind=="mineral"&&r.amount==1800)==4
                &&deposits.Count(r=>r.kind=="catalyst"&&r.amount==2400)==1,
                "Neon Loop expansion retains a complete 4m/1g camp: "+i);
            check(grid.CanPlace(sites[i],4)&&!map.resources.Any(r=>Inside(r,sites[i],4)),
                "Neon Loop expansion has a clear 4x4 headquarters pad: "+i);
            var gas=deposits.Single(r=>r.kind=="catalyst");var pad=new Point(gas.x-1,gas.y-1);
            check(grid.CanPlace(pad,3)&&map.resources.Count(r=>Inside(r,pad,3))==1,
                "Neon Loop expansion has a clear 3x3 extractor pad: "+i);
        }
        foreach(string faction in new[]{"industrial","biological"})
        {
            var sim=new Simulation(map,catalog,faction,faction);
            var hq=catalog.Role(faction,"hq");var extractor=catalog.Role(faction,"extractor");
            for(int player=0;player<2;player++)
            {
                var workers=sim.Entities.Values.Where(e=>e.owner==player&&sim.Type(e).role=="worker").Take(2).Select(e=>e.id).ToArray();
                check(sim.Submit(new GameCommand{player=player,kind=CommandKind.Move,entities=workers,
                    position=new Point(sites[player].x+(player==0?-1:5),sites[player].y+2)}).accepted,
                    "Neon Loop workers accept scouting the new expansion: "+faction+"/"+player);
            }
            for(int tick=0;tick<600;tick++)sim.Step();
            for(int player=0;player<2;player++)
            {
                sim.Players[player].minerals=10000;
                var workers=sim.Entities.Values.Where(e=>e.owner==player&&sim.Type(e).role=="worker").ToArray();
                check(sim.Submit(new GameCommand{player=player,kind=CommandKind.Build,entities=new[]{workers[0].id},
                    definition=hq.id,position=sites[player]}).accepted,
                    "Neon Loop expansion headquarters construction is accepted: "+faction+"/"+player);
                var gas=map.resources.Single(r=>r.id=="camp"+(player+2)+".catalyst");
                check(sim.Submit(new GameCommand{player=player,kind=CommandKind.Build,entities=new[]{workers[1].id},
                    definition=extractor.id,position=new Point(gas.x-1,gas.y-1)}).accepted,
                    "Neon Loop expansion extractor construction is accepted: "+faction+"/"+player);
            }
            for(int tick=0;tick<2000;tick++)sim.Step();
            check(sim.Entities.Values.Count(e=>e.definition==hq.id&&e.complete)==4
                &&sim.Entities.Values.Count(e=>e.definition==extractor.id&&e.complete)==2,
                "Neon Loop workers reach and finish both new headquarters and extractors: "+faction);
            for(int player=0;player<2;player++)
            {
                var workers=sim.Entities.Values.Where(e=>e.owner==player&&sim.Type(e).role=="worker").ToArray();
                var mineral=sim.Resources.First(r=>r.definition=="camp"+(player+2)+".mineral0");
                var gas=sim.Resources.Single(r=>r.definition=="camp"+(player+2)+".catalyst");
                check(sim.Submit(new GameCommand{player=player,kind=CommandKind.Gather,entities=new[]{workers[0].id},target=mineral.id}).accepted
                    &&sim.Submit(new GameCommand{player=player,kind=CommandKind.Gather,entities=new[]{workers[1].id},target=gas.id}).accepted,
                    "Neon Loop new camps accept mineral and gas harvesting: "+faction+"/"+player);
            }
            for(int tick=0;tick<1000;tick++)sim.Step();
            check(sim.Players.All(p=>p.catalyst>0)
                &&sim.Resources.Where(r=>r.definition=="camp2.mineral0"||r.definition=="camp3.mineral0").All(r=>r.remaining<1800),
                "Neon Loop both expansions harvest minerals and deliver gas: "+faction);
        }
    }
    private static bool Inside(ResourceDefinition resource,Point site,int size)
        =>resource.x>=site.x&&resource.y>=site.y&&resource.x<site.x+size&&resource.y<site.y+size;
}
