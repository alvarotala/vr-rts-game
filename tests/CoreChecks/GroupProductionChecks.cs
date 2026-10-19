using Frontier.Core;

static class GroupProductionChecks
{
    public static void Run(Catalog catalog,MapDefinition map,Action<bool,string> check)
    {
        foreach(string faction in new[]{"industrial","biological"})
        {
            var sim=new Simulation(map,catalog,faction);
            sim.Players[0].minerals=10000;sim.Players[0].catalyst=10000;
            for(int i=0;i<5;i++)Add("supply");
            var first=Add("barracks");var second=Add("barracks");
            var tech=Add("tech");var otherTech=Add("tech");var academy=Add("academy");
            var hq=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="hq");
            var worker=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="worker");
            var scout=catalog.Role(faction,"scout");var siege=catalog.Role(faction,"siege");
            var caster=catalog.Role(faction,"caster");int minerals=sim.Players[0].minerals;
            var group=new[]{second.id,first.id};

            Check(Submit(CommandKind.Train,scout.id,group)&&first.queue.Count==1&&second.queue.Count==0,
                "equal waits choose lowest building ID regardless of selection order");
            Check(Submit(CommandKind.Train,scout.id,group)&&first.queue.Count==1&&second.queue.Count==1,
                "second activation uses the idle barracks");
            Check(sim.Players[0].minerals==minerals-2*scout.minerals&&sim.Reserved(0)==2
                &&first.queue[0].id!=second.queue[0].id,
                "two activations reserve and charge exactly two units with distinct order IDs");

            SetQueue(first,scout.id,1000);SetQueue(second,scout.id,10,10,10);
            Check(Submit(CommandKind.Train,scout.id,group)&&first.queue.Count==1&&second.queue.Count==4,
                "remaining production time takes priority over queue length");
            SetQueue(first,scout.id,10,10);SetQueue(second,scout.id,20);
            Check(Submit(CommandKind.Train,scout.id,group)&&first.queue.Count==2&&second.queue.Count==2,
                "equal waits choose the shorter queue before building ID");
            SetQueue(first,scout.id,1000);SetQueue(second,scout.id,0,0,0,0,0);
            Check(Submit(CommandKind.Train,scout.id,group)&&first.queue.Count==2&&second.queue.Count==5,
                "full queues are skipped even when their remaining time is zero");

            var unfinished=Add("barracks");unfinished.complete=false;
            var dead=Add("barracks");dead.health=0;
            var supply=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="supply");
            first.queue.Clear();second.queue.Clear();
            Check(Submit(CommandKind.Train,scout.id,new[]{worker.id,hq.id,supply.id,unfinished.id,dead.id,second.id})
                &&second.queue.Count==1&&new[]{worker,hq,supply,unfinished,dead}.All(e=>e.queue.Count==0),
                "workers, wrong producers, unfinished buildings and dead producers are skipped");
            Rejected(CommandKind.Train,scout.id,new[]{worker.id,hq.id,supply.id,unfinished.id,dead.id},
                "selection without a compatible live completed producer cannot spend or enqueue");
            SetQueue(first,scout.id,1,1,1,1,1);SetQueue(second,scout.id,1,1,1,1,1);
            Rejected(CommandKind.Train,scout.id,group,"all full queues reject without spending");
            first.queue.Clear();second.queue.Clear();

            var mixed=new[]{hq.id,first.id,tech.id,academy.id};
            Check(Submit(CommandKind.Train,caster.id,mixed)&&academy.queue.Count==1&&tech.queue.Count==0&&first.queue.Count==0,
                "mixed producer selection routes casters to the academy");
            Check(Submit(CommandKind.Train,siege.id,mixed)&&tech.queue.Count==1&&hq.queue.Count==0,
                "mixed producer selection routes artillery to its producer");
            Check(Submit(CommandKind.Train,catalog.Role(faction,"worker").id,mixed)&&hq.queue.Count==1&&first.queue.Count==0,
                "mixed producer selection routes workers to headquarters");
            foreach(var e in sim.Entities.Values)e.queue.Clear();

            int gas=sim.Players[0].catalyst;minerals=sim.Players[0].minerals;
            SetQueue(tech,siege.id,1000);
            var researchGroup=new[]{first.id,tech.id,otherTech.id};var attack=catalog.Upgrade("attack");
            Check(Submit(CommandKind.Research,attack.id,researchGroup)&&otherTech.queue.Count==1
                &&otherTech.queue[0].research&&otherTech.queue[0].definition==attack.id&&tech.queue.Count==1,
                "research routes once to the compatible lab with the shortest wait");
            Check(sim.Players[0].minerals==minerals-attack.minerals&&sim.Players[0].catalyst==gas-attack.catalyst,
                "group research charges exactly one upgrade");
            Rejected(CommandKind.Research,attack.id,new[]{tech.id},"research queued at another selected or unselected lab cannot be duplicated");
            otherTech.queue.Clear();sim.Players[0].attackLevel=1;
            Rejected(CommandKind.Research,attack.id,researchGroup,"completed faction research cannot be duplicated");
            sim.Players[0].attackLevel=0;
            SetQueue(tech,siege.id,0,0,0,0,0);
            Check(Submit(CommandKind.Research,"armor",researchGroup)&&otherTech.queue.Count==1&&tech.queue.Count==5,
                "research skips a full lab");
            Rejected(CommandKind.Research,"unknown",researchGroup,"unknown research leaves economy and queues unchanged");
            Rejected(CommandKind.Research,attack.id,new[]{first.id,unfinished.id},"research requires a completed research building");
            foreach(var e in sim.Entities.Values)e.queue.Clear();

            sim.Players[0].minerals=0;
            Rejected(CommandKind.Train,scout.id,group,"group training respects mineral costs");
            Rejected(CommandKind.Research,attack.id,researchGroup,"group research respects mineral costs");
            sim.Players[0].minerals=10000;sim.Players[0].catalyst=0;
            Rejected(CommandKind.Train,siege.id,new[]{tech.id,otherTech.id},"group training respects catalyst costs");
            Rejected(CommandKind.Research,attack.id,researchGroup,"group research respects catalyst costs");
            sim.Players[0].catalyst=10000;
            Rejected(CommandKind.Train,catalog.Role(faction=="industrial"?"biological":"industrial","scout").id,group,
                "group training rejects the opposing faction's unit definition");
            Rejected(CommandKind.Train,"missing-unit",group,"unknown units cannot change any queue");
            var enemyHq=sim.Entities.Values.First(e=>e.owner==1&&sim.Type(e).role=="hq");
            Rejected(CommandKind.Train,scout.id,new[]{first.id,enemyHq.id},"mixed ownership rejects the whole command");

            int originalCapacity=sim.Capacity(0);
            foreach(var e in sim.Entities.Values.Where(e=>e.owner==0&&sim.Type(e).capacity>0))e.complete=false;
            Rejected(CommandKind.Train,scout.id,group,"group training respects unavailable supply");
            foreach(var e in sim.Entities.Values.Where(e=>e.owner==0&&sim.Type(e).capacity>0))e.complete=true;
            Check(sim.Capacity(0)==originalCapacity,"supply fixture restores the actual player capacity");

            minerals=sim.Players[0].minerals;
            Check(Submit(CommandKind.Train,scout.id,new[]{second.id})&&second.queue.Count==1&&first.queue.Count==0,
                "single selection stays on the selected producer even if another producer is free");
            int queued=second.queue[0].id;
            Rejected(CommandKind.Cancel,null,group,"group cancellation cannot choose an arbitrary building",queued);
            Check(Submit(CommandKind.Cancel,null,new[]{second.id},queued)&&second.queue.Count==0&&sim.Players[0].minerals==minerals,
                "single-building cancellation preserves its exact queue ID and full refund");
            Check(Submit(CommandKind.Train,scout.id,new[]{first.id,first.id})&&first.queue.Count==1,
                "duplicate selected IDs still queue only one unit");

            void Check(bool condition,string label)=>check(condition,"Group production "+faction+": "+label);
            EntityState Add(string role)
            {
                var type=catalog.Role(faction,role);int id=1000+sim.Entities.Count;
                var entity=new EntityState{id=id,owner=0,definition=type.id,health=type.health,position=new Point(25+(id%6)*4,12+(id%4)*4)};
                sim.Entities.Add(id,entity);return entity;
            }
            bool Submit(CommandKind kind,string definition,int[] ids,int queueOrderId=0)
                =>sim.Submit(new GameCommand{player=0,kind=kind,definition=definition,entities=ids,queueOrderId=queueOrderId}).accepted;
            string State()=>sim.Players[0].minerals+"/"+sim.Players[0].catalyst+"/"+string.Join(";",sim.Entities.Values.OrderBy(e=>e.id)
                .Select(e=>e.id+":"+string.Join(",",e.queue.Select(q=>q.id+"/"+q.definition+"/"+q.research+"/"+q.remaining))));
            void Rejected(CommandKind kind,string definition,int[] ids,string label,int queueOrderId=0)
            {
                string before=State();Check(!Submit(kind,definition,ids,queueOrderId)&&State()==before,label);
            }
            BlockedDeployment(catalog,faction,check);
        }
    }

    private static void BlockedDeployment(Catalog catalog,string faction,Action<bool,string> check)
    {
        foreach(string role in new[]{"barracks","tech"})
        {
            var map=new MapDefinition{id="group-production",title="Group production",width=80,height=64,
                spawns=new[]{new SpawnDefinition{player=0,x=2,y=2},new SpawnDefinition{player=1,x=72,y=56}},
                resources=new[]{new ResourceDefinition{id="left",x=10,y=55},new ResourceDefinition{id="right",x=69,y=8},
                    new ResourceDefinition{id="left-gas",kind="catalyst",x=12,y=55},new ResourceDefinition{id="right-gas",kind="catalyst",x=67,y=8}}};
            var sim=new Simulation(map,catalog,faction);sim.Players[0].minerals=10000;sim.Players[0].catalyst=10000;
            foreach(var worker in sim.Entities.Values.Where(e=>!sim.Type(e).building))worker.order=CommandKind.Hold;
            var supply=Add("supply",1000,new Point(15,15));
            var blocked=Add(role,1001,new Point(20,20));var working=Add(role,1002,new Point(30,20));
            var type=catalog.Role(faction,role=="barracks"?"scout":"siege");var group=new[]{blocked.id,working.id};
            if(role=="tech")
                Check(sim.Submit(new GameCommand{player=0,kind=CommandKind.SetRallyPoint,entities=group,position=new Point(40.5f,20.5f)}).accepted,
                    "rally producers accept a reachable destination before blocking");
            Check(Train(new[]{blocked.id})&&Train(new[]{working.id}),"two producers accept real initial unit orders");
            Border(blocked,9999);blocked.queue[0].remaining=1;working.queue[0].remaining=11;sim.Step();
            Check(blocked.queue.Count==1&&blocked.queue[0].remaining==0&&working.queue[0].remaining==10,
                "occupied perimeter really stops deployment while the alternative advances");
            int minerals=sim.Players[0].minerals,gas=sim.Players[0].catalyst;
            Check(Train(group)&&blocked.queue.Count==1&&working.queue.Count==2&&sim.Players[0].minerals==minerals-type.minerals
                &&sim.Players[0].catalyst==gas-type.catalyst,
                "group training chooses the advancing producer instead of the stalled zero-time queue");
            if(role=="tech")
                Check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Research,definition="attack",entities=group}).accepted
                    &&blocked.queue.Count==1&&working.queue.Count==3&&working.queue.Last().research,
                    "group research also avoids a lab stalled behind a finished unit");
            Check(Train(new[]{blocked.id})&&blocked.queue.Count==2,
                "explicit single-building training still accepts an order behind a blocked deployment");
            Border(working,9998);working.queue[0].remaining=1;sim.Step();
            int count=blocked.queue.Count+working.queue.Count;
            Check(working.queue[0].remaining==0&&Train(group)&&blocked.queue.Count+working.queue.Count==count+1,
                "a group with every exit blocked still queues exactly one item");
            Border(blocked,0);
            count=blocked.queue.Count;int units=sim.Units(0);sim.Step();
            Check(blocked.queue.Count==count-1&&sim.Units(0)==units+1&&blocked.queue[0].remaining==type.duration,
                "opening the exit deploys the waiting unit and preserves its next queued order");
            Check(Train(group)&&blocked.queue.Count==count&&working.queue[0].remaining==0,
                "group routing returns to the recovered producer while the other remains blocked");

            void Check(bool condition,string label)=>check(condition,"Group production blocked "+faction+"/"+role+": "+label);
            bool Train(int[] ids)=>sim.Submit(new GameCommand{player=0,kind=CommandKind.Train,definition=type.id,entities=ids}).accepted;
            EntityState Add(string buildingRole,int id,Point position)
            {
                var definition=catalog.Role(faction,buildingRole);
                var entity=new EntityState{id=id,owner=0,definition=definition.id,health=definition.health,position=position};
                sim.Entities.Add(id,entity);sim.Map.Occupy(position,definition.footprint,id);return entity;
            }
            void Border(EntityState producer,int occupant)
            {
                int size=sim.Type(producer).footprint,x=(int)producer.position.x,y=(int)producer.position.y;
                for(int dy=-1;dy<=size;dy++)for(int dx=-1;dx<=size;dx++)
                    if(dx==-1||dy==-1||dx==size||dy==size)sim.Map.Occupy(new Point(x+dx,y+dy),1,occupant);
            }
        }
    }

    private static void SetQueue(EntityState producer,string definition,params int[] remaining)
    {
        producer.queue.Clear();
        for(int i=0;i<remaining.Length;i++)producer.queue.Add(new ProductionOrder{id=100+i,definition=definition,remaining=remaining[i],total=Math.Max(1,remaining[i])});
    }
}
