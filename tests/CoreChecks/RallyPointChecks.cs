using Frontier.Core;

static class RallyPointChecks
{
    public static void Run(Catalog catalog, Action<bool,string> check)
    {
        Simulation Fixture(string faction = "industrial")
        {
            var map = new MapDefinition { id="rally-points", title="Rally point fixture", width=80, height=48,
                spawns=new[]{new SpawnDefinition{player=0,x=2,y=2},new SpawnDefinition{player=1,x=72,y=40}},
                resources=new[]{new ResourceDefinition{id="a",x=8,y=8,amount=10000},new ResourceDefinition{id="b",x=69,y=35,amount=10000},
                    new ResourceDefinition{id="c",kind="catalyst",x=10,y=8},new ResourceDefinition{id="d",kind="catalyst",x=74,y=35}} };
            var sim = new Simulation(map,catalog,faction);
            foreach(var unit in sim.Entities.Values.Where(e=>!sim.Type(e).building).ToArray())sim.Entities.Remove(unit.id);
            sim.Players[0].minerals=10000;sim.Players[0].catalyst=10000;
            return sim;
        }
        EntityState Add(Simulation sim,string role,Point position,int player=0)
        {
            var type=catalog.Role(sim.Players[player].faction,role);
            var entity=new EntityState{id=sim.Entities.Keys.Max()+1000,owner=player,definition=type.id,position=position,health=type.health};
            sim.Entities.Add(entity.id,entity);if(type.building)sim.Map.Occupy(position,type.footprint,entity.id);
            return entity;
        }
        EntityState Headquarters(Simulation sim,int player=0)=>sim.Entities.Values.Single(e=>e.owner==player&&sim.Type(e).role=="hq");
        bool Rally(Simulation sim,Point point,params EntityState[] producers)=>sim.Submit(new GameCommand{player=0,kind=CommandKind.SetRallyPoint,position=point,entities=producers.Select(e=>e.id).ToArray()}).accepted;
        bool Clear(Simulation sim,params EntityState[] producers)=>sim.Submit(new GameCommand{player=0,kind=CommandKind.ClearRallyPoint,entities=producers.Select(e=>e.id).ToArray()}).accepted;
        bool Train(Simulation sim,EntityState producer,string role)=>sim.Submit(new GameCommand{player=0,kind=CommandKind.Train,entities=new[]{producer.id},definition=catalog.Role(sim.Players[0].faction,role).id}).accepted;
        EntityState Finish(Simulation sim,EntityState producer)
        {
            var ids=new HashSet<int>(sim.Entities.Keys);producer.queue[0].remaining=1;sim.Step();
            return sim.Entities.Values.Single(e=>!ids.Contains(e.id));
        }
        void Steps(Simulation sim,int count){for(int n=0;n<count;n++)sim.Step();}

        check((int)CommandKind.Surrender==11&&(int)CommandKind.SetRallyPoint==12&&(int)CommandKind.ClearRallyPoint==13,"rally commands preserve existing serialized enum values");
        foreach(string faction in new[]{"industrial","biological"})
        {
            foreach(var type in catalog.Definition.archetypes.Where(a=>a.faction==faction))
                check(catalog.CanProduceUnits(type.id)==(type.role=="hq"||type.role=="barracks"||type.role=="tech"),"catalog identifies actual unit producer "+type.id);
            var sim=Fixture(faction);var hq=Headquarters(sim);var barracks=Add(sim,"barracks",new Point(20,15));var tech=Add(sim,"tech",new Point(20,25));Add(sim,"supply",new Point(20,5));
            var producers=new[]{hq,barracks,tech};var roles=new[]{"worker","ranged","siege"};Point destination=new Point(35.5f,20.5f);
            check(Rally(sim,destination,producers)&&producers.All(e=>e.hasRallyPoint&&e.rallyPoint.Distance(destination)<.001f),"rally supports all selected producers "+faction);
            for(int n=0;n<producers.Length;n++)
            {
                var producer=producers[n];check(Train(sim,producer,roles[n]),"rallied building accepts production "+producer.definition);
                int minerals=sim.Players[0].minerals,gas=sim.Players[0].catalyst;var queued=producer.queue[0];int progress=queued.remaining;
                check(Rally(sim,destination,producer)&&sim.Players[0].minerals==minerals&&sim.Players[0].catalyst==gas&&producer.queue.Count==1&&producer.queue[0]==queued&&queued.remaining==progress,
                    "setting rally changes neither cost nor production progress "+producer.definition);
                var born=Finish(sim,producer);
                check(born.order==CommandKind.Move&&born.resourceId==0&&born.destination.Distance(destination)<4&&producer.queue.Count==0,
                    "new unit starts normal movement to rally "+producer.definition);
                Point oldDestination=born.destination;
                check(Rally(sim,new Point(40.5f,30.5f),producer)&&born.destination.Distance(oldDestination)<.001f&&born.order==CommandKind.Move,
                    "changing rally never redirects existing units "+producer.definition);
                check(Train(sim,producer,roles[n]),"producer can queue after rally changes "+producer.definition);
                check(Rally(sim,new Point(42.5f,30.5f),producer),"rally can change while production is running "+producer.definition);
                var next=Finish(sim,producer);
                check(next.order==CommandKind.Move&&next.destination.Distance(producer.rallyPoint)<4&&born.destination.Distance(oldDestination)<.001f,
                    "latest rally is read when unit finishes "+producer.definition);
                check(Train(sim,producer,roles[n]),"production remains available before rally clear "+producer.definition);
                var beforeClear=producer.queue[0];int beforeClearProgress=beforeClear.remaining;
                check(Clear(sim,producer)&&!producer.hasRallyPoint&&producer.rallyPoint.Distance(default)==0&&next.order==CommandKind.Move
                    &&producer.queue.Count==1&&producer.queue[0]==beforeClear&&beforeClear.remaining==beforeClearProgress,
                    "clear removes marker and preserves existing moves "+producer.definition);
                var normal=Finish(sim,producer);
                check(normal.order==(roles[n]=="worker"?CommandKind.Gather:CommandKind.Stop),"cleared rally restores original spawn behavior "+producer.definition);
            }

            sim=Fixture(faction);hq=Headquarters(sim);destination=new Point(25.5f,16.5f);
            check(Rally(sim,destination,hq)&&Train(sim,hq,"worker"),"worker rally setup "+faction);
            var worker=Finish(sim,hq);Steps(sim,300);
            check(worker.order==CommandKind.Stop&&worker.position.Distance(worker.destination)<.2f&&worker.destination.Distance(destination)<.001f&&worker.resourceId==0,
                "worker reaches explicit rally without auto harvesting "+faction);
            check(sim.Submit(new GameCommand{kind=CommandKind.Move,player=0,entities=new[]{worker.id},position=new Point(28.5f,20.5f)}).accepted
                &&worker.order==CommandKind.Move&&worker.destination.Distance(destination)>1,"rallied unit accepts immediate player commands "+faction);

            sim=Fixture(faction);hq=Headquarters(sim);Rally(sim,destination,hq);var notices=new List<string>();sim.Notice+=(player,message)=>{if(player==0)notices.Add(message);};
            sim.Map.Occupy(new Point(25,16),1,99999);Train(sim,hq,"worker");worker=Finish(sim,hq);
            check(worker.order==CommandKind.Stop&&worker.resourceId==0&&sim.Map.CanStand(worker.position,sim.Type(worker).collisionRadius)&&hq.queue.Count==0
                &&notices.Any(n=>n.StartsWith("Rally point blocked.")),"blocked rally releases worker at safe exit with notice "+faction);

            sim=Fixture(faction);hq=Headquarters(sim);Rally(sim,destination,hq);Train(sim,hq,"worker");hq.queue[0].remaining=1;
            for(int y=1;y<=6;y++)for(int x=1;x<=6;x++)if(x==1||x==6||y==1||y==6)sim.Map.Occupy(new Point(x,y),1,99999);
            Steps(sim,5);
            check(sim.Units(0)==0&&hq.queue.Count==1&&hq.queue[0].remaining==0,"rally never bypasses blocked building exit "+faction);
            sim.Map.Occupy(new Point(1,1),1,0);sim.Step();worker=sim.Entities.Values.Single(e=>e.owner==0&&!sim.Type(e).building);
            check(hq.queue.Count==0&&worker.order==CommandKind.Move&&worker.position.Distance(new Point(1.5f,1.5f))<.001f,"finished rallied unit exits only after legal exit opens "+faction);
        }

        var fixture=Fixture();var base0=Headquarters(fixture);var destination0=new Point(25.5f,16.5f);
        check(!catalog.CanProduceUnits(null)&&!catalog.CanProduceUnits("missing"),"unknown catalog types cannot offer a rally action");
        check(Rally(fixture,new Point(25.1f,16.9f),base0)&&base0.rallyPoint.Distance(destination0)<.001f,"rally marker snaps to the same navigation cell used for movement");
        var supply0=Add(fixture,"supply",new Point(20,5));var worker0=Add(fixture,"worker",new Point(15.5f,15.5f));var unfinished=Add(fixture,"barracks",new Point(20,25));unfinished.complete=false;
        foreach(var invalid in new[]{supply0,worker0,unfinished,Headquarters(fixture,1)})
            check(!Rally(fixture,new Point(30.5f,20.5f),base0,invalid)&&base0.rallyPoint.Distance(destination0)<.001f&&!invalid.hasRallyPoint,
                "invalid mixed selection leaves every rally unchanged "+invalid.definition);
        foreach(var point in new[]{new Point(float.NaN,2),new Point(2,float.PositiveInfinity),new Point(-1,2),new Point(80,2),base0.position})
            check(!Rally(fixture,point,base0)&&base0.rallyPoint.Distance(destination0)<.001f,"invalid rally position preserves previous destination "+point.x+","+point.y);
        fixture.Paused=true;check(!Clear(fixture,base0)&&base0.hasRallyPoint,"paused match cannot change rally");fixture.Paused=false;
        check(!fixture.Submit(new GameCommand{player=1,kind=CommandKind.ClearRallyPoint,entities=new[]{base0.id}}).accepted&&base0.hasRallyPoint,"enemy cannot clear rally");
        var own=fixture.View(0).entities.Single(e=>e.id==base0.id);
        check(own.hasRallyPoint&&own.rallyPoint.Distance(destination0)<.001f,"own view exposes producer rally");
        own.rallyPoint=new Point(1,1);own.hasRallyPoint=false;
        check(base0.hasRallyPoint&&base0.rallyPoint.Distance(destination0)<.001f,"view edits cannot mutate rally state");
        var observer=Add(fixture,"worker",new Point(8.5f,5.5f),1);observer.order=CommandKind.Hold;Steps(fixture,4);
        var enemy=fixture.View(1).entities.Single(e=>e.id==base0.id);
        check(!enemy.remembered&&!enemy.hasRallyPoint&&enemy.rallyPoint.Distance(default)==0,"visible enemy building never reveals rally intent");
        observer.position=new Point(65.5f,35.5f);Steps(fixture,4);enemy=fixture.View(1).entities.Single(e=>e.id==base0.id);
        check(enemy.remembered&&!enemy.hasRallyPoint&&enemy.rallyPoint.Distance(default)==0,"remembered enemy building never reveals rally intent");

        fixture=Fixture();base0=Headquarters(fixture);Rally(fixture,destination0,base0);
        for(int y=0;y<fixture.Map.Height;y++)fixture.Map.Occupy(new Point(40,y),1,99999);
        check(!Rally(fixture,new Point(50.5f,16.5f),base0)&&base0.rallyPoint.Distance(destination0)<.001f,"disconnected walkable rally is rejected without changing prior point");
        var other=Add(fixture,"barracks",new Point(55,20));
        check(!Rally(fixture,new Point(30.5f,20.5f),base0,other)&&base0.rallyPoint.Distance(destination0)<.001f&&!other.hasRallyPoint,
            "multi producer reachability validates atomically before applying rally");

        fixture=Fixture();base0=Headquarters(fixture);Rally(fixture,new Point(50.5f,16.5f),base0);Train(fixture,base0,"worker");
        var failures=new List<string>();fixture.Notice+=(player,message)=>failures.Add(message);
        for(int y=0;y<fixture.Map.Height;y++)fixture.Map.Occupy(new Point(40,y),1,99999);
        var stranded=Finish(fixture,base0);
        check(stranded.order==CommandKind.Stop&&stranded.position.x<40&&base0.queue.Count==0&&failures.Any(n=>n.StartsWith("Rally point blocked.")),
            "new wall after rally assignment cannot teleport unit or stall production");

        fixture=Fixture();base0=Headquarters(fixture);
        foreach(var wall in new[]{new Point(0,1),new Point(1,0),new Point(1,2),new Point(2,1)})fixture.Map.Occupy(wall,1,99999);
        check(Rally(fixture,destination0,base0)&&Train(fixture,base0,"worker"),"rally remains valid when only the first exit is in an enclosed pocket");
        var connected=Finish(fixture,base0);
        check(connected.position.Distance(new Point(1.5f,1.5f))>.5f&&connected.order==CommandKind.Move,"producer chooses a connected exit instead of trapping a new unit in a pocket");

        fixture=Fixture();base0=Headquarters(fixture);Rally(fixture,destination0,base0);var group=new List<EntityState>();
        for(int i=0;i<6;i++){Train(fixture,base0,"worker");group.Add(Finish(fixture,base0));}
        check(group.Select(e=>fixture.Map.Cell(e.destination)).Distinct().Count()==group.Count,"consecutive rallied units reserve separate nearby destination slots");
        bool separated=true;
        for(int tick=0;tick<900&&group.Any(e=>e.order!=CommandKind.Stop);tick++)
        {
            fixture.Step();foreach(var a in group)foreach(var b in group)
                if(a.id<b.id&&a.position.Distance(b.position)<fixture.Type(a).collisionRadius+fixture.Type(b).collisionRadius-.001f)separated=false;
        }
        check(separated&&group.All(e=>e.order==CommandKind.Stop&&e.position.Distance(e.destination)<.2f),"consecutive rallied units arrive without overlaps or stranded orders");

        fixture=Fixture();base0=Headquarters(fixture);var hiddenPoint=new Point(50.5f,20.5f);
        check(Rally(fixture,hiddenPoint,base0),"unexplored terrain accepts rally before an unseen building exists");
        var hiddenBuilding=Add(fixture,"barracks",new Point(50,20),1);
        var actualOccupancy=(int[])fixture.Map.Occupant.Clone();
        check(!fixture.Visible(0,fixture.Center(hiddenBuilding))&&Rally(fixture,hiddenPoint,base0)&&base0.rallyPoint.Distance(hiddenPoint)<.001f,
            "unseen enemy covering rally target cannot change command acceptance");
        check(actualOccupancy.SequenceEqual(fixture.Map.Occupant),"fog-aware rally validation never mutates physical occupancy");
        var hiddenNotices=new List<string>();fixture.Notice+=(player,message)=>hiddenNotices.Add(message);
        Train(fixture,base0,"worker");var waitingForHiddenTarget=Finish(fixture,base0);
        check(waitingForHiddenTarget.order==CommandKind.Stop&&fixture.Map.CanStand(waitingForHiddenTarget.position,fixture.Type(waitingForHiddenTarget).collisionRadius)
            &&!hiddenNotices.Any(n=>n.StartsWith("Rally point blocked.")),"spawn cannot announce unseen enemy covering a remote rally target");
        observer=Add(fixture,"worker",new Point(45.5f,20.5f));observer.order=CommandKind.Hold;Steps(fixture,4);
        check(!Rally(fixture,hiddenPoint,base0),"currently visible enemy building blocks rally target");
        observer.position=new Point(10.5f,15.5f);Steps(fixture,4);
        check(fixture.View(0).entities.Single(e=>e.id==hiddenBuilding.id).remembered&&!Rally(fixture,hiddenPoint,base0),
            "remembered building footprint blocks rally target after losing vision");
        fixture.Entities.Remove(hiddenBuilding.id);fixture.Map.Occupy(hiddenBuilding.position,fixture.Type(hiddenBuilding).footprint,0);
        check(!Rally(fixture,hiddenPoint,base0),"hidden destruction cannot clear remembered rally obstruction");
        observer.position=new Point(45.5f,20.5f);Steps(fixture,4);
        check(Rally(fixture,hiddenPoint,base0),"observing the empty remembered footprint makes rally target available");

        fixture=Fixture();base0=Headquarters(fixture);var beyondCorridor=new Point(50.5f,21.5f);
        for(int y=0;y<fixture.Map.Height;y++)if(y<20||y>22)fixture.Map.Occupy(new Point(40,y),1,99999);
        check(Rally(fixture,beyondCorridor,base0),"unexplored open corridor accepts rally before enemy construction");
        hiddenBuilding=Add(fixture,"barracks",new Point(40,20),1);
        check(new Pathfinder(fixture.Map).Find(new Point(10.5f,10.5f),beyondCorridor)==null
            &&!fixture.Visible(0,fixture.Center(hiddenBuilding))&&Rally(fixture,beyondCorridor,base0),
            "unseen enemy sealing only corridor cannot change rally acceptance");
        hiddenNotices=new List<string>();fixture.Notice+=(player,message)=>hiddenNotices.Add(message);
        Train(fixture,base0,"worker");var waitingForHiddenCorridor=Finish(fixture,base0);
        check(waitingForHiddenCorridor.order==CommandKind.Stop&&waitingForHiddenCorridor.position.x<40
            &&!hiddenNotices.Any(n=>n.StartsWith("Rally point blocked.")),"spawn cannot announce unseen enemy blocking a remote corridor");
        observer=Add(fixture,"worker",new Point(35.5f,21.5f));observer.order=CommandKind.Hold;Steps(fixture,4);
        check(!Rally(fixture,beyondCorridor,base0),"visible enemy blocking corridor is included in rally route validation");
        observer.position=new Point(10.5f,15.5f);Steps(fixture,4);
        fixture.Entities.Remove(hiddenBuilding.id);fixture.Map.Occupy(hiddenBuilding.position,fixture.Type(hiddenBuilding).footprint,0);
        check(!Rally(fixture,beyondCorridor,base0),"remembered corridor remains blocked after unobserved destruction");
        observer.position=new Point(35.5f,21.5f);Steps(fixture,4);
        check(Rally(fixture,beyondCorridor,base0),"reobserving cleared corridor removes remembered route obstruction");
    }
}
