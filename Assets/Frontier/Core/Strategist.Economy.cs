using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Strategist
    {
        private GridMap economyTerrain;
        private Pathfinder economyPaths;
        private readonly Dictionary<int,int> producerReadyAt = new Dictionary<int,int>();
        private int economyBuildAt, economyResearchAt, economyBalanceAt, economyExpandAt;
        private int expansionWorker;
        private Point expansionResource;
        private bool planningExpansion;
        private int expansionStartedAt, expansionBlockedAt = -1;
        private readonly List<(Point position,int retryTick)> rejectedExpansionSites = new List<(Point,int)>();

        private IEnumerable<GameCommand> DecideEconomy(PlayerView view, VisibleEntity[] own)
        {
            var orders = new List<GameCommand>();
            var bases = own.Where(e=>e.health>0&&Type(e).role=="hq").OrderBy(e=>e.id).ToArray();
            var workers = own.Where(e=>e.health>0&&e.complete&&Type(e).role=="worker").OrderBy(e=>e.id).ToArray();
            if (bases.Length==0) return orders;
            if (economyTerrain==null) { economyTerrain=new GridMap(terrain.Definition); economyPaths=new Pathfinder(economyTerrain); }
            Array.Clear(economyTerrain.Occupant,0,economyTerrain.Occupant.Length);
            foreach(var building in view.entities.Where(e=>e.health>0&&Type(e).building))
                economyTerrain.Occupy(building.position,Type(building).footprint,building.id);
            var assignedWorkers = new HashSet<int>();
            var usedProducers = new HashSet<int>();
            var readyBases = bases.Where(e=>e.complete).ToArray();
            var localResources = view.resources.Where(r=>r.remaining!=0&&r.optimalWorkers>0
                &&readyBases.Any(b=>Center(b).Distance(r.position)<=Harvesting.BaseRadius)).ToArray();
            var mineralResources = localResources.Where(r=>r.kind=="mineral").ToArray();
            int saturation = localResources.Sum(r=>r.optimalWorkers);
            int workerGoal = Math.Min(pace.workers,Math.Max(8,saturation+2));
            int minerals=view.minerals,catalyst=view.catalyst,supply=view.supply;
            int Count(string role)=>own.Count(e=>e.health>0&&Type(e).role==role);
            bool Complete(string role)=>own.Any(e=>e.health>0&&e.complete&&Type(e).role==role);
            int CountUnits(string role)=>Count(role)+own.Sum(e=>e.queue.Count(q=>!q.research&&catalog[q.definition].role==role));
            bool Afford(Archetype t)=>minerals>=t.minerals&&catalyst>=t.catalyst;
            void Reserve(Archetype t){minerals-=t.minerals;catalyst-=t.catalyst;}
            bool Available(VisibleEntity w)=>!assignedWorkers.Contains(w.id)&&w.id!=expansionWorker
                &&w.order!=CommandKind.Build&&w.order!=CommandKind.Repair&&w.order!=CommandKind.Move;
            var producerCounts = catalog.Definition.archetypes.Where(t=>t.faction==view.faction&&!t.building)
                .ToDictionary(t=>t.role,t=>CountUnits(t.role));
            int plannedWorkers=Math.Max(workerGoal,producerCounts["worker"]);
            int armyGoal=Math.Min(Simulation.UnitLimit-plannedWorkers,pace.army+Math.Max(0,pace.workers-plannedWorkers));
            int militaryCount=producerCounts.Where(p=>p.Key!="worker").Sum(p=>p.Value);

            void AbandonExpansion()
            {
                rejectedExpansionSites.Add((expansionResource,view.tick+Seconds(180)));
                var scout=workers.FirstOrDefault(w=>w.id==expansionWorker);
                planningExpansion=false;expansionWorker=0;expansionBlockedAt=-1;
                if(scout==null)return;
                assignedWorkers.Add(scout.id);
                var resource=localResources.OrderBy(r=>scout.position.Distance(r.position)).FirstOrDefault(r=>
                    economyPaths.Find(scout.position,r.position,r.kind=="catalyst"?3:1.5f,null,Type(scout).collisionRadius)!=null);
                orders.Add(resource==null?Command(CommandKind.Stop,scout.id):Command(CommandKind.Gather,scout.id,target:resource.id));
            }
            rejectedExpansionSites.RemoveAll(s=>view.tick>=s.retryTick);

            if (planningExpansion && (bases.Any(b=>Center(b).Distance(expansionResource)<=Harvesting.BaseRadius)
                || !workers.Any(w=>w.id==expansionWorker)))
            { planningExpansion=false; expansionWorker=0; expansionBlockedAt=-1; }
            if(planningExpansion&&(own.Count(e=>Type(e).building)>=Simulation.BuildingLimit
                ||view.tick-expansionStartedAt>=Seconds(100)
                ||expansionBlockedAt>=0&&view.tick-expansionBlockedAt>=Seconds(20)
                ||view.resources.Any(r=>r.position.Distance(expansionResource)<.1f&&r.remaining==0)
                ||view.entities.Any(e=>e.owner!=player&&e.health>0&&Center(e).Distance(expansionResource)<18
                    &&(Type(e).building||!e.remembered&&Type(e).damage>0))))AbandonExpansion();

            // A replacement builder resumes paid foundations that need a worker instead of
            // cancelling investments whenever the first worker dies or is diverted.
            foreach(var building in own.Where(e=>e.health>0&&!e.complete&&Type(e).building))
            {
                if(workers.Any(w=>w.order==CommandKind.Build&&w.target==building.id))continue;
                if(catalog.Faction(view.faction).grows)
                { if(building.progress<=0)orders.Add(Command(CommandKind.Cancel,building.id));continue; }
                var builder=workers.Where(Available).OrderBy(w=>w.position.Distance(Center(building)))
                    .FirstOrDefault(w=>economyPaths.Find(w.position,Center(building),Type(building).footprint*.75f+1.2f,null,Type(w).collisionRadius)!=null);
                if(builder!=null){assignedWorkers.Add(builder.id);orders.Add(Command(CommandKind.Repair,builder.id,target:building.id));}
            }

            bool depleted=mineralResources.Length==0 || mineralResources.All(r=>r.remaining>=0)
                &&mineralResources.Sum(r=>r.remaining)<Math.Max(1400,workers.Length*140);
            bool expansionDue=depleted || view.tick>=Seconds(pace.expansion)&&workers.Length>=8;
            int maxBases=Difficulty==AiDifficulty.Hard?3:2;
            // Commit the expansion budget before optional buildings and military
            // queues consume it. Send its scout while those minerals accumulate.
            if(!planningExpansion&&bases.Length<maxBases&&own.Count(e=>Type(e).building)<Simulation.BuildingLimit&&bases.All(b=>b.complete)&&expansionDue
                &&view.tick>=economyExpandAt&&view.tick>=Seconds(Math.Min(pace.expansion,90))&&workers.Length>=6)
            {
                var opponent=terrain.Definition.spawns.First(s=>s.player!=player);
                Point enemyHome=new Point(opponent.x+2,opponent.y+2);
                var site=terrain.Definition.resources.Where(r=>r.kind=="mineral")
                    .Select(r=>new Point(r.x+.5f,r.y+.5f))
                    .Where(p=>bases.All(b=>Center(b).Distance(p)>Harvesting.BaseRadius+4)&&p.Distance(enemyHome)>22
                        &&!rejectedExpansionSites.Any(s=>s.position.Distance(p)<18)
                        &&!view.resources.Any(r=>r.position.Distance(p)<.1f&&r.remaining==0)
                        &&!view.entities.Any(e=>e.owner!=player&&e.health>0&&Center(e).Distance(p)<18
                            &&(Type(e).building||!e.remembered&&Type(e).damage>0)))
                    .OrderBy(p=>bases.Min(b=>Center(b).Distance(p))).FirstOrDefault();
                if(site.x!=0||site.y!=0)
                {
                    var scout=workers.Where(Available).OrderBy(w=>w.position.Distance(site)).FirstOrDefault();
                    if(scout!=null){expansionResource=site;expansionWorker=scout.id;planningExpansion=true;expansionStartedAt=view.tick;expansionBlockedAt=-1;}
                }
            }

            int plannedCapacity=own.Where(e=>!e.complete&&Type(e).role=="supply").Sum(e=>Type(e).capacity);
            var priorities=new List<string>();
            // Reserve enough supply for all ready producers, not just the next unit.
            int productionSlots=Math.Max(2,own.Count(e=>e.complete&&catalog.CanProduceUnits(e.definition)));
            if(view.capacity<Simulation.UnitLimit&&view.supply+productionSlots>=view.capacity+plannedCapacity)priorities.Add("supply");
            if(Count("barracks")==0)priorities.Add("barracks");
            int gasSites=view.resources.Count(r=>r.kind=="catalyst"&&r.remaining!=0
                &&readyBases.Any(b=>Center(b).Distance(r.position)<=Harvesting.BaseRadius));
            if(Count("extractor")<gasSites&&view.tick>=Seconds(pace.extractor))priorities.Add("extractor");
            if(Count("tech")==0&&Complete("barracks")&&view.tick>=Seconds(pace.tech)&&militaryCount>=6)priorities.Add("tech");
            if(Count("academy")==0&&Complete("tech")&&view.tick>=Seconds(pace.academy)&&militaryCount>=10)priorities.Add("academy");
            if(Count("research")==0&&Complete("barracks")&&view.tick>=Seconds(pace.research)-catalog.Role(view.faction,"research").duration&&militaryCount>=12)priorities.Add("research");
            int wantedBarracks=Math.Min(pace.barracks,1+readyBases.Length+(workers.Length>=18?1:0));
            if(Count("barracks")<wantedBarracks&&Complete("barracks")&&workers.Length>=8
                &&(minerals>=225||Complete("tech")))priorities.Add("barracks");
            int wantedTowers=Difficulty==AiDifficulty.Easy?readyBases.Length:Math.Min(readyBases.Length*2,readyBases.Length+(militaryCount>=20?1:0));
            if(Count("defense")<wantedTowers&&Complete("barracks")&&view.tick>=Seconds(pace.defense)
                &&(militaryCount>=12||view.entities.Any(e=>e.owner!=player&&!e.remembered&&bases.Any(b=>Center(b).Distance(e.position)<18))))
                priorities.Add("defense");

            int expansionCost=planningExpansion?catalog.Role(view.faction,"hq").minerals:0;
            int saveMinerals=0,builds=0;
            if(view.tick>=economyBuildAt)
            foreach(string role in priorities.Distinct())
            {
                if(builds>=2 || own.Count(e=>Type(e).building)+builds>=Simulation.BuildingLimit)break;
                var type=catalog.Role(view.faction,role);
                if(!string.IsNullOrEmpty(type.requires)&&!Complete(type.requires))continue;
                if(planningExpansion&&role!="supply"&&minerals-expansionCost<type.minerals)continue;
                var available=workers.Where(Available).ToArray();if(available.Length==0)break;
                var plan=readyBases.Select(b=>EconomyBuildSpot(view,own,available,type,b.position)).FirstOrDefault(p=>p.HasValue);
                if(!plan.HasValue)continue;
                if(!Afford(type))
                {
                    // Save for a reachable, almost affordable priority only when gas
                    // is already available. Missing tech never freezes all infantry.
                    if(catalyst>=type.catalyst&&(role=="supply"||role=="barracks"||role=="tech"||role=="academy"||role=="research"))
                        saveMinerals=Math.Max(saveMinerals,Math.Min(minerals,type.minerals));
                    continue;
                }
                var chosen=plan.Value;Reserve(type);assignedWorkers.Add(chosen.worker.id);builds++;
                economyTerrain.Occupy(chosen.position,type.footprint,-builds);
                orders.Add(Command(CommandKind.Build,chosen.worker.id,definition:type.id,position:chosen.position));
                economyBuildAt=view.tick+Seconds(pace.build);
            }

            if(planningExpansion)
            {
                var scout=workers.FirstOrDefault(w=>w.id==expansionWorker);
                var headquarters=catalog.Role(view.faction,"hq");
                if(scout!=null)
                {
                    assignedWorkers.Add(scout.id);
                    var plan=EconomyBuildSpot(view,own,new[]{scout},headquarters,expansionResource);
                    if(plan.HasValue)expansionBlockedAt=-1;
                    else if(scout.position.Distance(expansionResource)<10&&expansionBlockedAt<0)expansionBlockedAt=view.tick;
                    if(view.tick>=economyBuildAt&&Afford(headquarters)&&own.Count(e=>Type(e).building)+builds<Simulation.BuildingLimit)
                    {
                        if(plan.HasValue)
                        {
                            Reserve(headquarters);builds++;economyTerrain.Occupy(plan.Value.position,headquarters.footprint,-builds);
                            orders.Add(Command(CommandKind.Build,scout.id,definition:headquarters.id,position:plan.Value.position));
                            economyBuildAt=view.tick+Seconds(pace.build);economyExpandAt=view.tick+Seconds(Difficulty==AiDifficulty.Hard?180:240);
                            planningExpansion=false;expansionWorker=0;expansionBlockedAt=-1;
                        }
                    }
                    if(planningExpansion)
                    {
                        if(scout.order!=CommandKind.Move || scout.destination.Distance(expansionResource)>10)
                        {
                            var destination=EconomyScoutSpot(scout,expansionResource);
                            if(!destination.HasValue)AbandonExpansion();
                            else if(scout.position.Distance(destination.Value)>.4f)orders.Add(Command(CommandKind.Move,scout.id,position:destination.Value));
                        }
                    }
                }
            }
            if(planningExpansion)saveMinerals=Math.Max(saveMinerals,Math.Min(minerals,expansionCost));

            // Keep workers on useful local jobs; deliberate transfers can reach a
            // completed expansion when the old mineral line has run dry.
            var assignments=view.resources.ToDictionary(r=>r.id,r=>r.assignedWorkers);
            bool rebalance=view.tick>=economyBalanceAt;
            if(rebalance)economyBalanceAt=view.tick+Seconds(10);
            int gasTarget=view.catalyst>=200?1:Complete("tech")&&workers.Length>=10?2:1;
            foreach(var worker in workers.Where(w=>!assignedWorkers.Contains(w.id)&&w.id!=expansionWorker))
            {
                bool gathering=worker.order==CommandKind.Gather;
                if(worker.order!=CommandKind.Stop&&worker.order!=CommandKind.Hold&&!(gathering&&rebalance))continue;
                var current=view.resources.FirstOrDefault(r=>r.id==worker.target);
                if(gathering&&current!=null&&assignments.ContainsKey(current.id))assignments[current.id]=Math.Max(0,assignments[current.id]-1);
                var resource=localResources.OrderBy(r=>
                {
                    int target=r.kind=="catalyst"?gasTarget:r.optimalWorkers;
                    return (assignments[r.id]>=target?100:0)+assignments[r.id]*18f/target
                        +worker.position.Distance(r.position)*.25f+(r.kind=="catalyst"&&view.catalyst<100?-8:0)
                        +(current!=null&&current.id==r.id?-3:0);
                }).ThenBy(r=>r.id).FirstOrDefault(r=>economyPaths.Find(worker.position,r.position,r.kind=="catalyst"?3:1.5f,null,Type(worker).collisionRadius)!=null);
                if(resource!=null)
                {
                    assignments[resource.id]++;
                    if(!gathering||worker.target!=resource.id)orders.Add(Command(CommandKind.Gather,worker.id,target:resource.id));
                }
                else if(gathering&&current!=null&&assignments.ContainsKey(current.id))assignments[current.id]++;
            }

            if(!catalog.Faction(view.faction).regenerates&&minerals-saveMinerals>=40)
            {
                var damaged=own.Where(e=>e.health>0&&e.complete&&e.health<Type(e).health*.7f
                    &&(Type(e).building||e.order==CommandKind.Hold||e.order==CommandKind.Stop)
                    &&bases.Any(b=>Center(b).Distance(Center(e))<20)).OrderBy(e=>e.health/Type(e).health).FirstOrDefault();
                if(damaged!=null&&!workers.Any(w=>w.order==CommandKind.Repair&&w.target==damaged.id))
                {
                    var repairer=workers.Where(Available).OrderBy(w=>w.position.Distance(Center(damaged))).FirstOrDefault();
                    if(repairer!=null){assignedWorkers.Add(repairer.id);orders.Add(Command(CommandKind.Repair,repairer.id,target:damaged.id));}
                }
            }

            // Parallel production shares one reserved budget and counts existing
            // reservations. The unit role is chosen only from this producer's ready,
            // affordable options, so a missing advanced building cannot stall others.
            var workerType=catalog.Role(view.faction,"worker");
            foreach(var hq in readyBases.Where(b=>b.queue.Length==0))
            {
                if(producerCounts["worker"]>=workerGoal||supply>=view.capacity)break;
                if(!ProducerReady(hq.id,view.tick)||!Afford(workerType)||(minerals-saveMinerals<workerType.minerals&&workers.Length>=8))continue;
                Reserve(workerType);supply++;producerCounts["worker"]++;usedProducers.Add(hq.id);producerReadyAt[hq.id]=view.tick+Seconds(pace.recruit);
                orders.Add(Command(CommandKind.Train,hq.id,definition:workerType.id));
            }
            if(view.tick>=Seconds(pace.research)&&view.tick>=economyResearchAt&&producerCounts["siege"]>0&&militaryCount>=16)
            {
                var upgrade=catalog.Definition.upgrades.FirstOrDefault(u=>!view.researched.Contains(u.id)&&!own.Any(e=>e.queue.Any(q=>q.research&&q.definition==u.id)));
                var research=own.FirstOrDefault(e=>e.complete&&Type(e).role=="research"&&e.queue.Length<=1&&!e.queue.Any(q=>q.research));
                if(upgrade!=null&&research!=null&&minerals-saveMinerals>=upgrade.minerals&&catalyst>=upgrade.catalyst)
                {
                    minerals-=upgrade.minerals;catalyst-=upgrade.catalyst;usedProducers.Add(research.id);economyResearchAt=view.tick+Seconds(30);
                    orders.Add(Command(CommandKind.Research,research.id,definition:upgrade.id));
                }
            }
            var readyProducers=own.Where(e=>e.complete&&e.health>0&&Type(e).building&&Type(e).role!="hq"&&e.queue.Length==0
                &&!usedProducers.Contains(e.id)&&ProducerReady(e.id,view.tick)).ToList();
            while(readyProducers.Count>0)
            {
                int army=producerCounts.Where(p=>p.Key!="worker").Sum(p=>p.Value);
                if(army>=armyGoal||supply>=view.capacity)break;
                var candidate=catalog.Definition.archetypes.Where(t=>!t.building&&t.faction==view.faction&&t.role!="worker"
                    &&readyProducers.Any(p=>Type(p).role==t.producer)&&(string.IsNullOrEmpty(t.requires)||Complete(t.requires))&&catalyst>=t.catalyst)
                    .OrderBy(t=>producerCounts[t.role]/EconomyWeight(t.role)).ThenBy(t=>t.role=="ranged"?0:t.role=="melee"?1:2).ThenBy(t=>t.minerals).FirstOrDefault();
                if(candidate==null)break;
                // Save the next mineral delivery for the biggest unlocked army
                // deficit. Four cheap barracks must not starve the first caster,
                // nor should expensive support drain a missing infantry frontline.
                if(minerals-saveMinerals<candidate.minerals)break;
                var producer=readyProducers.First(p=>Type(p).role==candidate.producer);readyProducers.Remove(producer);
                Reserve(candidate);supply++;producerCounts[candidate.role]++;producerReadyAt[producer.id]=view.tick+Seconds(pace.recruit);
                orders.Add(Command(CommandKind.Train,producer.id,definition:candidate.id));
            }
            return orders;
        }

        private bool ProducerReady(int id,int tick)=>!producerReadyAt.TryGetValue(id,out int ready)||tick>=ready;
        private static float EconomyWeight(string role)=>role=="ranged"||role=="melee"?3:role=="caster"?1.5f:role=="siege"?1.25f:1;

        private (Point position,VisibleEntity worker)? EconomyBuildSpot(PlayerView view,VisibleEntity[] own,VisibleEntity[] workers,Archetype type,Point origin)
        {
            var spawn=terrain.Definition.spawns.First(s=>s.player!=player);
            var opponent=new Point(spawn.x+2,spawn.y+2);
            var home=new Point(origin.x+2,origin.y+2);
            Point desired=type.role=="defense"?Point.Towards(home,opponent,4)
                :type.role=="hq"?origin:Point.Towards(home,new Point(home.x*2-opponent.x,home.y*2-opponent.y),5);
            IEnumerable<Point> candidates;
            if(type.role=="extractor")candidates=view.resources.Where(r=>r.kind=="catalyst"&&r.remaining!=0&&r.extractorId==0
                &&own.Any(b=>b.complete&&Type(b).role=="hq"&&Center(b).Distance(r.position)<=Harvesting.BaseRadius))
                .Select(r=>new Point((int)r.position.x-1,(int)r.position.y-1));
            else
            {
                var points=new List<Point>();
                for(int y=Math.Max(0,(int)origin.y-16);y<=Math.Min(terrain.Height-type.footprint,(int)origin.y+16);y++)
                    for(int x=Math.Max(0,(int)origin.x-16);x<=Math.Min(terrain.Width-type.footprint,(int)origin.x+16);x++)points.Add(new Point(x,y));
                candidates=points.OrderBy(p=>new Point(p.x+type.footprint*.5f,p.y+type.footprint*.5f).Distance(desired));
            }
            foreach(var point in candidates)
            {
                if(!economyTerrain.CanPlace(point,type.footprint))continue;
                var center=new Point(point.x+type.footprint*.5f,point.y+type.footprint*.5f);
                if(center.Distance(opponent)<(type.role=="defense"?21:16))continue;
                if(type.role=="hq"&&(center.Distance(origin)>12||own.Any(b=>Type(b).role=="hq"&&Center(b).Distance(center)<20)))continue;
                bool visible=true;
                for(int y=(int)point.y;y<point.y+type.footprint&&visible;y++)for(int x=(int)point.x;x<point.x+type.footprint;x++)
                    if(view.visibility[economyTerrain.Index(x,y)]!=2){visible=false;break;}
                if(!visible)continue;
                // All currently visible mobile bodies count, including the builder.
                if(view.entities.Any(e=>!e.remembered&&e.health>0&&!Type(e).building&&EconomyOverlaps(e.position,Type(e).collisionRadius,point,type.footprint)))continue;
                bool neighbors=false;
                for(int y=Math.Max(0,(int)point.y-1);y<=Math.Min(terrain.Height-1,(int)point.y+type.footprint)&&!neighbors;y++)
                    for(int x=Math.Max(0,(int)point.x-1);x<=Math.Min(terrain.Width-1,(int)point.x+type.footprint);x++)
                        if(economyTerrain.Occupant[economyTerrain.Index(x,y)]!=0){neighbors=true;break;}
                if(neighbors&&type.role!="extractor")continue;
                var deposits=terrain.Definition.resources.Where(r=>r.x>=point.x-1&&r.x<point.x+type.footprint+1&&r.y>=point.y-1&&r.y<point.y+type.footprint+1).ToArray();
                if(type.role=="extractor")
                {if(deposits.Count(r=>r.kind=="catalyst"&&r.x>=point.x&&r.x<point.x+type.footprint&&r.y>=point.y&&r.y<point.y+type.footprint)!=1)continue;}
                else if(deposits.Length>0||terrain.Definition.resources.Any(r=>r.kind=="catalyst"
                    &&r.x>=point.x-2&&r.x<point.x+type.footprint+2&&r.y>=point.y-2&&r.y<point.y+type.footprint+2))continue;
                var worker=workers.OrderBy(w=>w.position.Distance(center)).FirstOrDefault(w=>economyPaths.Find(w.position,center,type.footprint*.75f+1.2f,null,Type(w).collisionRadius)!=null);
                if(worker!=null)return(point,worker);
            }
            return null;
        }
        private Point? EconomyScoutSpot(VisibleEntity worker,Point resource)
        {
            for(int radius=4;radius<=8;radius++)for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)
            {
                if(Math.Abs(dx)!=radius&&Math.Abs(dy)!=radius)continue;
                var point=new Point((int)resource.x+dx+.5f,(int)resource.y+dy+.5f);
                if(economyTerrain.CanStand(point,Type(worker).collisionRadius)&&economyPaths.Find(worker.position,point,.1f,null,Type(worker).collisionRadius)!=null)return point;
            }
            return null;
        }
        private static bool EconomyOverlaps(Point unit,float radius,Point origin,int size)
        {
            var nearest=new Point(Math.Max(origin.x,Math.Min(origin.x+size,unit.x)),Math.Max(origin.y,Math.Min(origin.y+size,unit.y)));
            return unit.Distance(nearest)<radius;
        }
    }
}
