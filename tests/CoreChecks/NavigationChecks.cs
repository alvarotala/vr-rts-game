using Frontier.Core;

static class NavigationChecks
{
    public static void Run(Catalog catalog, MapDefinition rift, Action<bool,string> check)
    {
        Simulation Fixture(string faction="industrial",MapDefinition authored=null)
        {
            var map=authored??new MapDefinition{id="traffic",title="Traffic fixture",width=80,height=48,
                spawns=new[]{new SpawnDefinition{player=0,x=2,y=2},new SpawnDefinition{player=1,x=72,y=40}},
                resources=new[]{new ResourceDefinition{id="a",x=10,y=12,amount=10000},new ResourceDefinition{id="b",x=69,y=35,amount=10000},new ResourceDefinition{id="c",kind="catalyst",x=8,y=8},new ResourceDefinition{id="d",kind="catalyst",x=74,y=35}}};
            var s=new Simulation(map,catalog,faction);
            foreach(var unit in s.Entities.Values.Where(e=>!s.Type(e).building).ToArray())s.Entities.Remove(unit.id);
            return s;
        }
        EntityState Unit(Simulation s,float x,float y,string role="worker",int owner=0)
        {
            string faction=s.Players[owner].faction;var type=catalog.Role(faction,role);
            var e=new EntityState{id=s.Entities.Keys.Max()+1,owner=owner,definition=type.id,position=new Point(x,y),health=type.health};
            s.Entities.Add(e.id,e);return e;
        }
        bool Move(Simulation s,IEnumerable<EntityState> units,Point goal)=>s.Submit(new GameCommand{player=0,kind=CommandKind.Move,entities=units.Select(e=>e.id).ToArray(),position=goal}).accepted;
        void Run(Simulation s,int ticks,string scenario)
        {
            for(int tick=0;tick<ticks;tick++)
            {
                var units=s.Entities.Values.Where(e=>!s.Type(e).building).ToArray();
                var before=units.ToDictionary(e=>e.id,e=>e.position);
                s.Step();
                foreach(var u in units)
                {
                    if(!s.Map.CanStand(u.position,s.Type(u).collisionRadius))throw new Exception($"{scenario}: {u.id} enters solid terrain at {u.position.x},{u.position.y}");
                    if(before[u.id].Distance(u.position)>s.Type(u).speed/20+.002f)throw new Exception(scenario+": push exceeds speed budget");
                    foreach(var v in units)if(u.id<v.id&&u.position.Distance(v.position)<s.Type(u).collisionRadius+s.Type(v).collisionRadius-.001f)
                        throw new Exception($"{scenario}: overlapping bodies {u.id}/{v.id} at tick {s.Tick}");
                }
            }
        }
        void Arrived(Simulation s,IEnumerable<EntityState> group,string label)
        {
            var missed=group.Where(e=>e.position.Distance(e.destination)>.15f).ToArray();
            if(missed.Length>0)Console.WriteLine(label+" remaining: "+string.Join("; ",missed.Take(8).Select(e=>$"{e.id} {e.position.x:F2},{e.position.y:F2}->{e.destination.x:F1},{e.destination.y:F1} {e.order} blocked={e.blockedTicks}")));
            check(missed.Length==0,label);
        }
        var sim=Fixture();var a=Unit(sim,20.1f,20.5f);
        check(Move(sim,new[]{a},new Point(20.5f,20.5f)),"same-cell destination accepted");Run(sim,30,"same cell");Arrived(sim,new[]{a},"same-cell movement finishes");

        sim=Fixture();a=Unit(sim,20.5f,20.5f);var idle=Unit(sim,21.5f,20.5f);
        for(int x=5;x<36;x++){sim.Map.Walkable[sim.Map.Index(x,19)]=false;sim.Map.Walkable[sim.Map.Index(x,21)]=false;}
        check(Move(sim,new[]{a},new Point(25.5f,20.5f)),"move behind idle ally accepted");Run(sim,400,"idle push");
        Arrived(sim,new[]{a},"idle ally yields in narrow lane");check(idle.position.Distance(new Point(21.5f,20.5f))>1,"friendly push displaces idle ally gradually");

        sim=Fixture();a=Unit(sim,20.5f,20.5f);idle=Unit(sim,22.5f,20.5f);idle.order=CommandKind.Hold;
        Move(sim,new[]{a},new Point(26.5f,20.5f));Run(sim,400,"hold detour");
        Arrived(sim,new[]{a},"moving unit routes around hold-position ally");check(idle.position.Distance(new Point(22.5f,20.5f))<.001f,"hold-position unit is not pushed");

        sim=Fixture();a=Unit(sim,20.5f,20.5f);var enemy=Unit(sim,22.5f,20.5f,owner:1);enemy.order=CommandKind.Hold;
        for(int y=0;y<48;y++)if(y!=20)sim.Map.Walkable[sim.Map.Index(22,y)]=false;
        Move(sim,new[]{a},new Point(25.5f,20.5f));Run(sim,200,"enemy lane");
        check(a.position.x<22&&enemy.position.Distance(new Point(22.5f,20.5f))<.001f,"enemies remain solid and cannot be pushed through a choke");

        sim=Fixture();a=Unit(sim,20.5f,20.5f);enemy=Unit(sim,22.5f,20.5f,owner:1);Run(sim,4,"enemy vision");
        enemy.order=CommandKind.Move;enemy.destination=new Point(24.5f,22.5f);
        Move(sim,new[]{a},enemy.destination);
        check(a.destination.Distance(enemy.destination)<.01f,"formation planning does not read a visible enemy's private move destination");

        sim=Fixture();a=Unit(sim,19.8f,20.5f);Run(sim,4,"foundation vision");
        int beforeBuild=sim.Players[0].minerals;
        check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Build,entities=new[]{a.id},definition="industrial.supply",position=new Point(20,20)}).accepted
            &&sim.Players[0].minerals==beforeBuild-catalog["industrial.supply"].minerals
            &&sim.Map.CanStand(a.position,sim.Type(a).collisionRadius),"foundation reserves safely when only the edge of an allied body overlaps");
        Run(sim,200,"foundation clearance");
        check(sim.Map.Occupant[sim.Map.Index(20,20)]!=0&&sim.Map.CanStand(a.position,sim.Type(a).collisionRadius),
            "edge obstruction walks clear before foundation blocks terrain");

        foreach(string faction in new[]{"industrial","biological"})
        {
            sim=Fixture(faction);var group=new List<EntityState>();string[] roles={"worker","scout","melee","ranged","siege"};
            for(int n=0;n<Simulation.UnitLimit;n++)group.Add(Unit(sim,10.5f+n%10,15.5f+n/10,roles[n%5]));
            check(Move(sim,group,new Point(43.5f,25.5f)),Simulation.UnitLimit+"-unit move accepted "+faction);
            check(group.Select(e=>sim.Map.Cell(e.destination)).Distinct().Count()==Simulation.UnitLimit,"maximum group receives unique destinations "+faction);
            Run(sim,1400,"large group "+faction);Arrived(sim,group,"all "+Simulation.UnitLimit+" mixed units arrive without overlap "+faction);
            if(faction=="industrial")
            {
                Move(sim,group,new Point(79.5f,.5f));
                check(group.All(e=>e.order==CommandKind.Move)&&group.Select(e=>sim.Map.Cell(e.destination)).Distinct().Count()==Simulation.UnitLimit,"all units at the population limit receive slots at the map corner");
                Run(sim,1400,"corner group");Arrived(sim,group,"formation fits at the map edge without stranding units");
            }
        }
        sim=Fixture();var left=new List<EntityState>();var right=new List<EntityState>();
        for(int n=0;n<8;n++){left.Add(Unit(sim,20.5f+n%2,20.5f+n/2));right.Add(Unit(sim,35.5f+n%2,20.5f+n/2));}
        Move(sim,left,new Point(38.5f,22.5f));Move(sim,right,new Point(17.5f,22.5f));Run(sim,1000,"opposite traffic");
        Arrived(sim,left.Concat(right),"opposing allied groups pass without a permanent jam");

        sim=Fixture();a=Unit(sim,18.5f,20.5f);var b=Unit(sim,26.5f,20.5f);
        for(int x=20;x<=24;x++){sim.Map.Walkable[sim.Map.Index(x,19)]=false;sim.Map.Walkable[sim.Map.Index(x,21)]=false;}
        Move(sim,new[]{a},new Point(28.5f,20.5f));Move(sim,new[]{b},new Point(16.5f,20.5f));Run(sim,800,"head-on choke");
        Arrived(sim,new[]{a,b},"head-on units yield at a one-cell corridor");

        sim=Fixture();a=Unit(sim,18.5f,20.5f);Move(sim,new[]{a},new Point(32.5f,20.5f));Run(sim,10,"before building");
        sim.Map.Occupy(new Point(22,19),3,999);Run(sim,500,"new building");Arrived(sim,new[]{a},"route updates when a building blocks the original path");

        sim=Fixture(authored:rift);var rampGroup=new List<EntityState>();
        for(int n=0;n<20;n++)rampGroup.Add(Unit(sim,43.5f+n%4,36.5f+n/4,n%2==0?"worker":"ranged"));
        Move(sim,rampGroup,new Point(66.5f,40.5f));Run(sim,1400,"ramp crowd");Arrived(sim,rampGroup,"group crosses ramp without cutting cliffs");

        sim=Fixture();var miners=new List<EntityState>();for(int n=0;n<12;n++)miners.Add(Unit(sim,8.5f+n%4,15.5f+n/4));
        var deposit=sim.Resources.First(r=>r.definition=="a");
        Run(sim,4,"initial vision");
        check(sim.Submit(new GameCommand{kind=CommandKind.Gather,player=0,entities=miners.Select(e=>e.id).ToArray(),target=deposit.id}).accepted,"twelve workers assigned to the same deposit");
        var delivered=new HashSet<int>();int income=sim.Players[0].minerals;
        for(int tick=0;tick<2400;tick++)
        {
            var cargo=miners.ToDictionary(e=>e.id,e=>e.cargo);Run(sim,1,"mining traffic");
            foreach(var miner in miners)if(cargo[miner.id]>0&&miner.cargo==0)delivered.Add(miner.id);
        }
        check(delivered.Count==12&&sim.Players[0].minerals-income>=96,"every worker completes gather/deposit cycles despite congestion");
    }
}
