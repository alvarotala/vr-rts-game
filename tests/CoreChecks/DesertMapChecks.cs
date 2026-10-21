using Frontier.Core;

static class DesertMapChecks
{
    private static readonly Point[] Sites={new(18,18),new(138,138),new(22,62),new(134,94),new(46,110),new(110,46),new(70,30),new(86,126)};

    public static void Run(Catalog catalog,MapDefinition map,MapDefinition rift,Action<bool,string> check)
    {
        var grid=new GridMap(map);
        check(map.schemaVersion==1&&map.id=="desert"&&map.theme=="desert"&&map.width==160&&map.height==160
            &&grid.Count>rift.width*rift.height,"Desert: Amber Wastes is a larger v1 desert map");
        var groups=map.resources.GroupBy(r=>r.id.Split('.')[0]).ToArray();
        check(groups.Length==8&&map.resources.Length==64,"Desert: exactly eight resource zones, including both starts");
        for(int i=0;i<8;i++)
        {
            var camp=groups.Single(g=>g.Key=="camp"+i).ToArray();
            check(camp.Count(r=>r.kind=="mineral")==6&&camp.Count(r=>r.kind=="catalyst")==2
                &&camp.All(r=>r.amount==(r.kind=="mineral"?1800:2400)),"Desert: six minerals and two gas deposits at camp"+i);
            check(grid.CanPlace(Sites[i],4)&&camp.All(r=>!Inside(r,Sites[i],4)),"Desert: flat, resource-free 4x4 headquarters pad at camp"+i);
            var center=new Point(Sites[i].x+2,Sites[i].y+2);
            check(camp.All(r=>new Point(r.x+.5f,r.y+.5f).Distance(center)<10),"Desert: every deposit sits near the camp"+i+" headquarters pad");
            foreach(var gas in camp.Where(r=>r.kind=="catalyst"))
            {
                var pad=new Point(gas.x-1,gas.y-1);
                check(grid.CanPlace(pad,3)&&map.resources.Count(r=>Inside(r,pad,3))==1,"Desert: complete 3x3 extractor pad for "+gas.id);
            }
        }
        bool mirrored=true;
        for(int i=0;i<grid.Count;i++)
        {
            int opposite=grid.Count-1-i;
            mirrored&=grid.Walkable[i]==grid.Walkable[opposite]&&grid.Buildable[i]==grid.Buildable[opposite]
                &&grid.Elevation[i]==grid.Elevation[opposite]&&grid.Ramp[i]==grid.Ramp[opposite]
                &&Math.Abs(grid.SurfaceLevel[i]-grid.SurfaceLevel[opposite])<.0001f;
        }
        check(mirrored,"Desert: exact 180-degree terrain, ramp, elevation and buildability symmetry");
        check(map.resources.All(a=>map.resources.Any(b=>b.x==map.width-1-a.x&&b.y==map.height-1-a.y&&b.kind==a.kind&&b.amount==a.amount)),
            "Desert: every mineral and gas deposit has an identical rotated counterpart");
        check(map.decoration.All(a=>!grid.Walkable[grid.Index(a.x,a.y)]
            &&map.decoration.Any(b=>b.x==map.width-1-a.x&&b.y==map.height-1-a.y&&Math.Abs((b.rotation-a.rotation+360)%360-180)<.001f)),
            "Desert: paired sandstone decoration stays on blocked terrain");
        var ramps=map.terrain.Where(t=>t.ramp).ToArray();
        check(ramps.Length==20&&ramps.All(r=>(r.rampAxis=="x"?r.height:r.width)>=12),"Desert: twenty ramps provide broad twelve-cell army entrances");
        // A broad ramp is not useful if it empties into a thin trench. Leave an
        // eight-cell-deep, flat approach across the whole mouth of every ramp.
        foreach(var ramp in ramps)
        {
            bool xAxis=ramp.rampAxis=="x",clear=true;
            int depth=xAxis?ramp.width:ramp.height,span=xAxis?ramp.height:ramp.width;
            for(int step=0;step<8;step++)for(int across=0;across<span;across++)
            {
                int along=ramp.rampDirection>0?-step-1:depth+step;
                int x=ramp.x+(xAxis?along:across),y=ramp.y+(xAxis?across:along);
                clear&=FlatLowCell(x,y);
            }
            check(clear,"Desert: full-width eight-cell low landing at ramp "+ramp.x+","+ramp.y);
        }
        // The central mesa used to overlap these shelves at two-cell corners,
        // closing the low route into awkward pockets beside the inner ramps.
        foreach(var corner in new[]{(x:88,y:60),(x:60,y:92)})
        {
            bool clear=true;
            for(int y=corner.y;y<corner.y+8;y++)for(int x=corner.x;x<corner.x+12;x++)clear&=FlatLowCell(x,y);
            check(clear,"Desert: broad low passage separates the central mesa and expansion at "+corner.x+","+corner.y);
        }
        check(grid.Elevation.Count(e=>e>0)>3000&&map.terrain.Count(t=>!t.walkable)>=17,"Desert: distinct mesas and rock outcrops add substantial relief");

        // Occupy every intended HQ and extractor pad, then verify that the
        // largest collision radius can still cross all camps and approach gas.
        var developed=new GridMap(map);int occupant=1;
        foreach(var site in Sites)developed.Occupy(site,4,occupant++);
        foreach(var gas in map.resources.Where(r=>r.kind=="catalyst"))developed.Occupy(new Point(gas.x-1,gas.y-1),3,occupant++);
        float radius=catalog.Definition.archetypes.Where(t=>!t.building).Max(t=>t.collisionRadius);
        var paths=new Pathfinder(developed);
        foreach(var spawn in map.spawns)
        {
            var start=new Point(spawn.x+2.5f,spawn.y-2.5f);
            foreach(var resource in map.resources)
            {
                var target=new Point(resource.x+.5f,resource.y+.5f);
                var route=paths.Find(start,target,resource.kind=="catalyst"?3.25f:.1f,null,radius);
                bool clear=route!=null;var previous=start;
                if(route!=null)foreach(int cell in route){var next=developed.Center(cell);clear&=developed.CanTraverse(previous,next,radius);previous=next;}
                check(clear,"Desert: wide artillery from player"+spawn.player+" reaches "+resource.id+" with developed camps");
            }
        }
        StartingParity(catalog,map,check);
        Construction(catalog,map,check);
        Movement(catalog,map,check);

        bool FlatLowCell(int x,int y)
        {
            if(!grid.Inside(x,y))return false;
            int cell=grid.Index(x,y);
            return grid.Walkable[cell]&&!grid.Ramp[cell]&&grid.SurfaceLevel[cell]==0;
        }
    }

    private static bool Inside(ResourceDefinition resource,Point origin,int size)
        =>resource.x>=origin.x&&resource.x<origin.x+size&&resource.y>=origin.y&&resource.y<origin.y+size;

    private static void StartingParity(Catalog catalog,MapDefinition map,Action<bool,string> check)
    {
        var first=new Simulation(map,catalog,"industrial","biological");var swapped=new Simulation(map,catalog,"biological","industrial");
        check(first.Players[0].minerals==first.Players[1].minerals&&first.Players[0].catalyst==first.Players[1].catalyst
            &&first.Units(0)==6&&first.Units(1)==6&&first.Buildings(0)==1&&first.Buildings(1)==1&&first.Capacity(0)==first.Capacity(1),
            "Desert: identical starting economy, workers, headquarters and supply");
        foreach(var entity in first.Entities.Values.Where(e=>e.owner==0))
        {
            var point=first.Center(entity);var mirrored=new Point(map.width-point.x,map.height-point.y);
            check(first.Entities.Values.Any(other=>other.owner==1&&first.Type(other).role==first.Type(entity).role
                &&first.Center(other).Distance(mirrored)<.001f&&other.health==entity.health),"Desert: rotated starting "+first.Type(entity).role+" position and health");
        }
        bool equal=true;
        for(int tick=0;tick<800;tick++)
        {
            first.Step();swapped.Step();
            for(int player=0;player<2;player++)equal&=first.Players[player].minerals==swapped.Players[player].minerals&&first.Players[player].catalyst==swapped.Players[player].catalyst;
            foreach(var entity in first.Entities.Values)
            {
                var other=swapped.Entities[entity.id];equal&=entity.position.Distance(other.position)<.0001f&&entity.health==other.health&&entity.cargo==other.cargo;
            }
        }
        check(equal&&first.Players.All(p=>p.minerals>400),"Desert: real starting harvesting is unchanged by swapping factions in either slot");
    }

    private static void Construction(Catalog catalog,MapDefinition map,Action<bool,string> check)
    {
        foreach(string faction in new[]{"industrial","biological"})
        {
            for(int camp=2;camp<8;camp++)
            {
                var sim=new Simulation(map,catalog,faction,faction);var site=Sites[camp];
                var worker=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="worker");
                worker.position=new Point(site.x+2.5f,site.y-1.5f);worker.order=CommandKind.Hold;worker.resourceId=0;
                for(int tick=0;tick<4;tick++)sim.Step();
                var hq=catalog.Role(faction,"hq");
                check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Build,entities=new[]{worker.id},definition=hq.id,position=site}).accepted,
                    "Desert: real headquarters order accepted at camp"+camp+" / "+faction);
                for(int tick=0;tick<hq.duration+80;tick++)sim.Step();
                check(sim.Entities.Values.Any(e=>e.owner==0&&e.definition==hq.id&&e.complete&&e.position.Distance(site)<.01f),
                    "Desert: worker finishes expansion headquarters at camp"+camp+" / "+faction);
            }
            var gasSim=new Simulation(map,catalog,faction,faction);var assigned=new List<(int player,int worker,int gas)>();
            foreach(int player in new[]{0,1})
            {
                var workers=gasSim.Entities.Values.Where(e=>e.owner==player&&gasSim.Type(e).role=="worker").Take(2).ToArray();
                var deposits=gasSim.Resources.Where(r=>r.definition.StartsWith("camp"+player+".")&&r.kind=="catalyst").ToArray();
                var extractor=catalog.Role(faction,"extractor");
                for(int i=0;i<2;i++)
                {
                    var position=new Point(deposits[i].position.x-1.5f,deposits[i].position.y-1.5f);
                    check(gasSim.Submit(new GameCommand{player=player,kind=CommandKind.Build,entities=new[]{workers[i].id},definition=extractor.id,position=position}).accepted,
                        "Desert: starting gas"+i+" accepts extractor for player"+player+" / "+faction);
                    assigned.Add((player,workers[i].id,deposits[i].id));
                }
            }
            for(int tick=0;tick<1000;tick++)gasSim.Step();
            check(gasSim.Entities.Values.Count(e=>gasSim.Type(e).role=="extractor"&&e.complete)==4,"Desert: all four starting extractors finish / "+faction);
            foreach(var order in assigned)check(gasSim.Submit(new GameCommand{player=order.player,kind=CommandKind.Gather,entities=new[]{order.worker},target=order.gas}).accepted,
                "Desert: completed starting gas accepts harvesting / "+faction);
            for(int tick=0;tick<800;tick++)gasSim.Step();
            check(gasSim.Players.All(p=>p.catalyst>0),"Desert: both players actually deliver gas / "+faction);
        }
    }

    private static void Movement(Catalog catalog,MapDefinition map,Action<bool,string> check)
    {
        foreach(string faction in new[]{"industrial","biological"})
        {
            var sim=new Simulation(map,catalog,faction,faction);var type=catalog.Role(faction,"siege");
            var artillery=new EntityState{id=1000,owner=0,definition=type.id,position=new Point(25.5f,15.5f),health=type.health,order=CommandKind.Hold};
            sim.Entities.Add(artillery.id,artillery);
            foreach(var destination in new[]{new Point(68.5f,80.5f),new Point(48.5f,112.5f),new Point(72.5f,32.5f)})
            {
                check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Move,entities=new[]{artillery.id},position=destination}).accepted,
                    "Desert: siege accepts route across mesas / "+faction);
                for(int tick=0;tick<4000&&artillery.position.Distance(destination)>.7f;tick++)sim.Step();
                check(artillery.position.Distance(destination)<.7f,"Desert: siege physically crosses ramps and reaches mesa / "+faction);
            }
        }
    }
}
