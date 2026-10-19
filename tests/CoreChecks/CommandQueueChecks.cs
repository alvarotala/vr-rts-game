using Frontier.Core;

static class CommandQueueChecks
{
    public static void Run(Catalog catalog, Action<bool,string> check)
    {
        Simulation Fixture(string faction="industrial")
        {
            var map=new MapDefinition{id="command-queues",title="Command queue fixture",width=80,height=48,
                spawns=new[]{new SpawnDefinition{player=0,x=2,y=2},new SpawnDefinition{player=1,x=72,y=40}},
                resources=new[]{new ResourceDefinition{id="a",x=10,y=12,amount=10000},new ResourceDefinition{id="b",x=69,y=35,amount=10000},new ResourceDefinition{id="c",kind="catalyst",x=8,y=8},new ResourceDefinition{id="d",kind="catalyst",x=74,y=35}}};
            var s=new Simulation(map,catalog,faction);
            foreach(var e in s.Entities.Values.Where(e=>!s.Type(e).building).ToArray())s.Entities.Remove(e.id);
            return s;
        }
        EntityState Unit(Simulation s,Point position,string role="worker",int owner=0)
        {
            var type=catalog.Role(s.Players[owner].faction,role);
            var e=new EntityState{id=1000+s.Entities.Count,owner=owner,definition=type.id,position=position,health=type.health};
            s.Entities.Add(e.id,e);return e;
        }
        GameCommand Order(EntityState unit,CommandKind kind,int sequence,Point position=default,bool append=false,int target=0,int replace=0,string definition=null)
            =>new GameCommand{player=unit.owner,entities=new[]{unit.id},kind=kind,sequence=sequence,position=position,append=append,target=target,replacePendingSequence=replace,definition=definition};
        void Steps(Simulation s,int count){for(int i=0;i<count;i++)s.Step();}
        bool Until(Simulation s,Func<bool> predicate,int timeout=1600){for(int i=0;i<timeout&&!predicate();i++)s.Step();return predicate();}

        var sim=Fixture();var unit=Unit(sim,new Point(20.5f,20.5f));
        check(sim.Submit(Order(unit,CommandKind.Move,1,new Point(30.5f,20.5f))).accepted,"queue first move starts immediately");
        Steps(sim,15);Point moving=unit.position,velocity=unit.velocity,first=unit.destination;
        check(sim.Submit(Order(unit,CommandKind.Move,2,new Point(30.5f,30.5f),true)).accepted
            &&sim.Submit(Order(unit,CommandKind.Move,3,new Point(40.5f,30.5f),true)).accepted,"two waypoints enqueue behind moving unit");
        check(unit.activeOrderSequence==1&&unit.destination.Distance(first)<.001f&&unit.position.Distance(moving)<.001f&&unit.velocity.Distance(velocity)<.001f,
            "appending waypoints preserves active destination and momentum");
        check(Until(sim,()=>unit.activeOrderSequence==2)&&unit.position.Distance(first)<.2f,"FIFO starts second action only after reaching first waypoint");
        Point second=unit.destination;
        check(Until(sim,()=>unit.activeOrderSequence==3)&&unit.position.Distance(second)<.2f,"FIFO reaches the second waypoint before the third");
        check(Until(sim,()=>unit.order==CommandKind.Stop)&&unit.pendingOrders.Count==0&&unit.position.Distance(new Point(40.5f,30.5f))<.2f,"queued path completes all waypoints");

        sim=Fixture();unit=Unit(sim,new Point(20.5f,20.5f));
        sim.Submit(Order(unit,CommandKind.Move,1,new Point(30.5f,20.5f)));
        sim.Submit(Order(unit,CommandKind.Move,2,new Point(30.5f,30.5f),true));
        sim.Submit(Order(unit,CommandKind.Move,3,new Point(40.5f,30.5f),true));
        check(sim.Submit(Order(unit,CommandKind.AttackMove,4,new Point(30.5f,30.5f),true,replace:2)).accepted
            &&unit.pendingOrders.Count==2&&unit.pendingOrders[0].kind==CommandKind.AttackMove&&unit.pendingOrders[0].sourceSequence==4&&unit.pendingOrders[1].sourceSequence==3,
            "double A upgrades the matching pending move in place without changing FIFO order");
        check(sim.Submit(Order(unit,CommandKind.AttackMove,5,new Point(30.5f,20.5f),true,replace:1)).accepted
            &&unit.order==CommandKind.AttackMove&&unit.activeOrderSequence==5&&unit.pendingOrders.Count==2,"double A upgrades an active first tap while retaining later actions");
        check(!sim.Submit(Order(unit,CommandKind.Move,6,new Point(-10,20))).accepted&&unit.pendingOrders.Count==2&&unit.activeOrderSequence==5,
            "invalid immediate destination does not erase queued actions");
        check(sim.Submit(Order(unit,CommandKind.Move,7,new Point(25.5f,25.5f))).accepted&&unit.pendingOrders.Count==0&&unit.activeOrderSequence==7,
            "ordinary move replaces the current action and clears pending actions");
        sim.Submit(Order(unit,CommandKind.Move,8,new Point(30.5f,30.5f),true));
        check(sim.Submit(Order(unit,CommandKind.Hold,9,append:true)).accepted&&unit.order==CommandKind.Hold&&unit.pendingOrders.Count==0,
            "Hold Position remains immediate and clears queued actions");
        sim.Submit(Order(unit,CommandKind.Move,10,new Point(30.5f,20.5f),true));
        check(unit.order==CommandKind.Move&&unit.activeOrderSequence==10&&unit.pendingOrders.Count==0,"append on an idle or holding unit starts immediately");
        sim.Submit(Order(unit,CommandKind.Move,11,new Point(30.5f,30.5f),true));
        check(sim.Submit(Order(unit,CommandKind.Stop,12,append:true)).accepted&&unit.order==CommandKind.Stop&&unit.pendingOrders.Count==0,"Stop clears action queue even with modifier held");

        sim=Fixture();unit=Unit(sim,new Point(20.5f,20.5f));
        sim.Submit(Order(unit,CommandKind.Move,1,new Point(30.5f,20.5f)));
        bool capacity=true;for(int n=0;n<Simulation.UnitOrderLimit;n++)capacity&=sim.Submit(Order(unit,CommandKind.Move,n+2,new Point(30.5f,25.5f+n%2),true)).accepted;
        check(capacity&&unit.pendingOrders.Count==32,"unit action queue accepts 32 pending orders");
        check(!sim.Submit(Order(unit,CommandKind.Move,34,new Point(40.5f,20.5f),true)).accepted&&unit.pendingOrders.Count==32,"full action queue rejects without removing earlier orders");
        check(sim.Submit(Order(unit,CommandKind.AttackMove,35,new Point(30.5f,25.5f),true,replace:2)).accepted&&unit.pendingOrders.Count==32,"upgrading a queued move still works at the queue limit");
        var own=sim.View(0).entities.Single(e=>e.id==unit.id);
        check(own.pendingOrders.Length==32&&own.activeOrderSequence==1&&own.destination.Distance(unit.destination)<.001f,"own player view contains current destination and queued actions");
        own.pendingOrders[0].target=999;own.pendingOrders[0].position=new Point(-10,-10);
        check(unit.pendingOrders[0].target==0&&unit.pendingOrders[0].position.x>=0,"player view returns copies of queued action data");
        var enemyObserver=Unit(sim,new Point(22.5f,20.5f),owner:1);enemyObserver.order=CommandKind.Hold;Steps(sim,4);
        var observed=sim.View(1).entities.Single(e=>e.id==unit.id);
        check(observed.pendingOrders.Length==0&&observed.activeOrderSequence==0&&observed.destination.Distance(default)==0,"enemy view never reveals queued or active movement intent");
        var foreign=Order(unit,CommandKind.Move,36,new Point(40,40),true);foreign.player=1;
        check(!sim.Submit(foreign).accepted&&unit.pendingOrders.Count==32,"queue validates ownership before mutating orders");

        foreach(bool hidden in new[]{false,true})
        {
            sim=Fixture();unit=Unit(sim,new Point(20.5f,20.5f));var enemy=Unit(sim,new Point(24.5f,20.5f),owner:1);enemy.order=CommandKind.Hold;Steps(sim,4);
            sim.Submit(Order(unit,CommandKind.Move,1,new Point(20.5f,26.5f)));
            check(sim.Submit(Order(unit,CommandKind.Attack,2,append:true,target:enemy.id)).accepted,"visible attack target can be queued "+hidden);
            sim.Submit(Order(unit,CommandKind.Move,3,new Point(26.5f,26.5f),true));
            var notices=new List<string>();sim.Notice+=(player,message)=>notices.Add(message);
            if(hidden)enemy.position=new Point(65.5f,35.5f);else sim.Entities.Remove(enemy.id);
            check(Until(sim,()=>unit.activeOrderSequence==3)&&notices.Any(n=>n.StartsWith("Queued Attack skipped:")),"invalid queued attack is skipped and next action proceeds "+hidden);
            check(!sim.Submit(Order(unit,CommandKind.Attack,4,append:true,target:enemy.id)).accepted,"cannot enqueue a hidden or deleted enemy "+hidden);
        }
        sim=Fixture();unit=Unit(sim,new Point(20.5f,20.5f));var target=Unit(sim,new Point(22.5f,20.5f),owner:1);target.order=CommandKind.Hold;Steps(sim,4);
        sim.Submit(Order(unit,CommandKind.Move,1,new Point(28.5f,20.5f)));
        sim.Submit(Order(unit,CommandKind.Move,2,new Point(30.5f,25.5f),true));
        check(sim.Submit(Order(unit,CommandKind.Attack,3,append:true,target:target.id,replace:1)).accepted&&unit.order==CommandKind.Attack&&unit.pendingOrders.Count==1,
            "double A can upgrade an active context move to targeted attack");
        target.health=1;
        check(Until(sim,()=>unit.activeOrderSequence==2)&&!sim.Entities.ContainsKey(target.id),"targeted attack advances queue after target dies");

        foreach(string faction in new[]{"industrial","biological"})
        {
            sim=Fixture(faction);unit=Unit(sim,new Point(16.5f,20.5f));Steps(sim,4);sim.Players[0].minerals=1000;
            var supply=catalog.Role(faction,"supply");int cash=sim.Players[0].minerals;
            sim.Submit(Order(unit,CommandKind.Move,1,new Point(17.5f,20.5f)));
            check(sim.Submit(Order(unit,CommandKind.Build,2,new Point(20,19),true,definition:supply.id)).accepted&&sim.Players[0].minerals==cash&&sim.Buildings(0)==1,
                "queued construction reserves no resources and creates no foundation "+faction);
            sim.Submit(Order(unit,CommandKind.Move,3,new Point(15.5f,26.5f),true));
            check(Until(sim,()=>unit.activeOrderSequence==2)&&sim.Players[0].minerals==cash-supply.minerals&&sim.Buildings(0)==2,"queued construction pays once on activation "+faction);
            var foundation=sim.Entities.Values.Single(e=>e.definition==supply.id);
            check(Until(sim,()=>unit.activeOrderSequence==3)&&foundation.complete,
                "construction retains its worker until completion before advancing the queue "+faction);
            check(sim.Players[0].minerals==cash-supply.minerals,"finishing queued construction never charges twice "+faction);
        }
        sim=Fixture();unit=Unit(sim,new Point(16.5f,20.5f));Steps(sim,4);sim.Players[0].minerals=0;
        sim.Submit(Order(unit,CommandKind.Move,1,new Point(17.5f,20.5f)));
        check(sim.Submit(Order(unit,CommandKind.Build,2,new Point(20,19),true,definition:"industrial.supply")).accepted,"future build can enqueue before funds are available");
        sim.Submit(Order(unit,CommandKind.Move,3,new Point(15.5f,26.5f),true));
        check(Until(sim,()=>unit.activeOrderSequence==3)&&sim.Players[0].minerals==0&&sim.Buildings(0)==1,"unaffordable queued construction skips safely on activation");

        sim=Fixture();unit=Unit(sim,new Point(10.5f,14.5f));Steps(sim,4);int starting=sim.Players[0].minerals;
        var mineral=sim.Resources.First(r=>r.kind=="mineral");
        sim.Submit(Order(unit,CommandKind.Gather,1,target:mineral.id));
        sim.Submit(Order(unit,CommandKind.Move,2,new Point(18.5f,17.5f),true));
        check(Until(sim,()=>unit.activeOrderSequence==2)&&sim.Players[0].minerals==starting+8&&unit.cargo==0&&mineral.remaining>0,
            "gather with a pending action completes one delivery then advances");
        sim=Fixture();unit=Unit(sim,new Point(10.5f,14.5f));Steps(sim,4);var ally=Unit(sim,new Point(12.5f,14.5f));ally.health-=8;
        sim.Submit(Order(unit,CommandKind.Repair,1,target:ally.id));
        sim.Submit(Order(unit,CommandKind.Move,2,new Point(18.5f,17.5f),true));
        check(Until(sim,()=>unit.activeOrderSequence==2)&&ally.health==sim.Type(ally).health,"repair completes target before advancing pending move");

        sim=Fixture();unit=Unit(sim,new Point(20.5f,20.5f));
        sim.Submit(Order(unit,CommandKind.Move,1,new Point(23.5f,20.5f)));
        sim.Submit(Order(unit,CommandKind.Move,2,new Point(30.5f,20.5f),true));
        sim.Submit(Order(unit,CommandKind.Move,3,new Point(35.5f,25.5f),true));
        var blockedNotices=new List<string>();sim.Notice+=(player,message)=>blockedNotices.Add(message);
        sim.Map.Occupy(new Point(30,20),1,999);
        check(Until(sim,()=>unit.activeOrderSequence==3)&&blockedNotices.Any(n=>n.StartsWith("Queued Move skipped:")),
            "waypoint blocked after enqueue is revalidated and skipped without stranding the queue");
        sim=Fixture();unit=Unit(sim,new Point(20.5f,20.5f));
        sim.Submit(Order(unit,CommandKind.Move,1,unit.position));Steps(sim,1);
        check(sim.Submit(Order(unit,CommandKind.AttackMove,2,new Point(25.5f,25.5f),true,replace:1)).accepted
            &&unit.order==CommandKind.AttackMove&&unit.activeOrderSequence==2&&unit.pendingOrders.Count==0,
            "double A still attacks if a very short first move already completed");

        sim=Fixture();var group=Enumerable.Range(0,8).Select(i=>Unit(sim,new Point(15.5f+i%4,20.5f+i/4))).ToArray();
        check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Move,sequence=1,entities=group.Select(e=>e.id).ToArray(),position=new Point(30.5f,22.5f)}).accepted,
            "group current move accepted for queued formation");
        check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Move,sequence=2,entities=group.Select(e=>e.id).ToArray(),position=new Point(45.5f,28.5f),append=true}).accepted
            &&group.Select(e=>sim.Map.Cell(e.pendingOrders[0].position)).Distinct().Count()==group.Length
            &&group.Select(e=>e.pendingOrders[0].trafficPriority).Distinct().Count()==1,"queued group retains distinct destination slots and one traffic cohort");
        bool noOverlap=true;
        for(int i=0;i<1400&&group.Any(e=>e.order!=CommandKind.Stop||e.pendingOrders.Count>0);i++)
        {
            sim.Step();foreach(var a in group)foreach(var b in group)if(a.id<b.id&&a.position.Distance(b.position)<sim.Type(a).collisionRadius+sim.Type(b).collisionRadius-.001f)noOverlap=false;
        }
        check(noOverlap&&group.All(e=>e.order==CommandKind.Stop&&e.pendingOrders.Count==0&&e.position.Distance(e.destination)<.2f),
            "group completes queued formation without overlapping or stranding members");
    }
}
