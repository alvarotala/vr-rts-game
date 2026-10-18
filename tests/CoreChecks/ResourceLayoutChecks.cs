using Frontier.Core;

static class ResourceLayoutChecks
{
    public static void Run(Catalog catalog, IEnumerable<MapDefinition> maps, Action<bool,string> check)
    {
        foreach(var map in maps)
        {
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
        }
    }
}
