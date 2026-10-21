using Frontier.Core;

static class AiEconomyChecks
{
    public static void Run(Catalog catalog,MapDefinition map,MapDefinition training,Action<bool,string> check)
    {
        foreach(string faction in new[]{"industrial","biological"})
        {
            {
                var sim=Fixture(catalog,faction);var first=Add(sim,0,"barracks",new Point(3,12));var second=Add(sim,0,"barracks",new Point(13,4));
                Add(sim,0,"supply",new Point(3,4));Add(sim,0,"supply",new Point(13,9));
                Add(sim,0,"ranged",new Point(18.5f,12.5f));Add(sim,0,"melee",new Point(20.5f,12.5f));Add(sim,0,"scout",new Point(22.5f,12.5f));
                Warm(sim);sim.Players[0].minerals=1500;sim.Players[0].catalyst=0;
                var bot=new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition);var commands=Decide(bot,sim,0);
                var trainingOrders=commands.Where(c=>c.kind==CommandKind.Train&&c.entities[0]!=Headquarters(sim).id).ToArray();
                check(trainingOrders.Length==2&&trainingOrders.Select(c=>c.entities[0]).ToHashSet().SetEquals(new[]{first.id,second.id}),
                    faction+" AI economy: both barracks train despite missing siege and caster producers");
                check(trainingOrders.All(c=>catalog[c.definition].producer=="barracks"),faction+" AI economy: available infantry composition never requests locked technology");
                Submit(sim,commands,check,faction+" available production");int before=sim.Units(0);Steps(sim,320);
                check(sim.Units(0)>=before+2&&first.queue.Count==0&&second.queue.Count==0,faction+" AI economy: parallel infantry actually deploys");
            }
            {
                var sim=Fixture(catalog,faction);var producers=new List<EntityState>();
                foreach(var point in new[]{new Point(3,12),new Point(13,4),new Point(17,4),new Point(3,17)})producers.Add(Add(sim,0,"barracks",point));
                producers.Add(Add(sim,0,"tech",new Point(17,9)));producers.Add(Add(sim,0,"academy",new Point(17,14)));
                Add(sim,0,"supply",new Point(3,4));Add(sim,0,"supply",new Point(13,9));Add(sim,0,"supply",new Point(13,12));
                Warm(sim);sim.Players[0].minerals=5000;sim.Players[0].catalyst=5000;
                var bot=new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition);var commands=Decide(bot,sim,60);
                var trainingOrders=commands.Where(c=>c.kind==CommandKind.Train&&producers.Any(p=>p.id==c.entities[0])).ToArray();
                check(trainingOrders.Length==producers.Count&&trainingOrders.Any(c=>catalog[c.definition].role=="siege")&&trainingOrders.Any(c=>catalog[c.definition].role=="caster"),
                    faction+" AI economy: infantry, siege and caster producers operate in the same decision");
                Submit(sim,commands,check,faction+" complete production");Steps(sim,560);
                check(sim.Entities.Values.Any(e=>e.owner==0&&sim.Type(e).role=="siege")&&sim.Entities.Values.Any(e=>e.owner==0&&sim.Type(e).role=="caster"),
                    faction+" AI economy: advanced parallel reservations become real units");

                // A fresh strategy must reserve the same finite balance across all
                // producers, even when several could independently afford a unit.
                foreach(var producer in producers)producer.queue.Clear();sim.Players[0].minerals=260;sim.Players[0].catalyst=0;
                commands=Decide(bot,sim,120);
                int mineralCost=commands.Where(c=>c.kind==CommandKind.Build||c.kind==CommandKind.Train).Sum(c=>catalog[c.definition].minerals);
                check(mineralCost<=260,faction+" AI economy: parallel construction and production share a reserved mineral budget");
                Submit(sim,commands,check,faction+" finite budget");check(sim.Players[0].minerals>=0&&sim.Units(0)+sim.Reserved(0)<=sim.Capacity(0),
                    faction+" AI economy: actual submission respects resources and supply");
            }
            {
                var sim=Fixture(catalog,faction);sim.Players[0].minerals=1000;sim.Players[0].catalyst=1000;
                var original=Decide(new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition),sim,60).First(c=>c.kind==CommandKind.Build);
                var blockedPoint=original.position;var blockedType=catalog[original.definition];
                Add(sim,0,"worker",new Point(blockedPoint.x+blockedType.footprint*.5f,blockedPoint.y+blockedType.footprint*.5f));Warm(sim);
                var commands=Decide(new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition),sim,60);
                var buildings=commands.Where(c=>c.kind==CommandKind.Build).ToArray();
                check(buildings.Length>=1&&buildings.All(c=>c.position.Distance(blockedPoint)>.1f),faction+" AI economy: occupied mobile footprint gets a different build site");
                check(buildings.Select(c=>c.entities[0]).Distinct().Count()==buildings.Length,
                    faction+" AI economy: each simultaneous foundation reserves its own builder");
                check(buildings.All(c=>!sim.Entities.Values.Any(e=>e.health>0&&!sim.Type(e).building&&Overlaps(e.position,sim.Type(e).collisionRadius,c.position,catalog[c.definition].footprint))),
                    faction+" AI economy: all visible mobile bodies are excluded from planned foundations");
                for(int i=0;i<buildings.Length;i++)for(int j=i+1;j<buildings.Length;j++)
                    check(!RectanglesOverlap(buildings[i].position,catalog[buildings[i].definition].footprint,buildings[j].position,catalog[buildings[j].definition].footprint),
                        faction+" AI economy: simultaneous build footprints are reserved before the next plan");
                Submit(sim,commands,check,faction+" blocked building site");
            }
            {
                var sim=Fixture(catalog,faction);Add(sim,0,"barracks",new Point(6,19));Warm(sim);sim.Players[0].minerals=1000;
                var bot=new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition);var commands=Decide(bot,sim,60);
                check(commands.Any(c=>c.kind==CommandKind.Build&&catalog[c.definition].role=="extractor"),
                    faction+" AI economy: adjacent producer does not permanently block the legal gas extractor footprint");
                Submit(sim,commands,check,faction+" adjacent gas extractor");Steps(sim,420);
                check(sim.HasRole(0,"extractor"),faction+" AI economy: adjacent gas extractor completes");
                commands=Decide(bot,sim,90);Submit(sim,commands,check,faction+" new gas harvesting");Steps(sim,400);
                check(sim.Players[0].catalyst>0,faction+" AI economy: completed extractor becomes real catalyst income");
            }
            {
                var sim=Fixture(catalog,faction);Add(sim,0,"barracks",new Point(3,12));Add(sim,0,"tech",new Point(13,4));Add(sim,0,"academy",new Point(17,4));
                Add(sim,0,"supply",new Point(3,4));Add(sim,0,"supply",new Point(13,9));Add(sim,0,"supply",new Point(17,9));
                for(int i=0;i<6;i++){Add(sim,0,"ranged",new Point(25.5f+i,8.5f));Add(sim,0,"melee",new Point(25.5f+i,10.5f));}
                Add(sim,0,"scout",new Point(25.5f,12.5f));Add(sim,0,"scout",new Point(27.5f,12.5f));
                Add(sim,0,"siege",new Point(25.5f,14.5f));Add(sim,0,"siege",new Point(27.5f,14.5f));
                Headquarters(sim).queue.Add(new ProductionOrder{definition=catalog.Role(faction,"worker").id,remaining=1000,total=1000});
                Warm(sim);sim.Players[0].minerals=140;sim.Players[0].catalyst=100;
                var bot=new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition);var commands=Decide(bot,sim,0);
                check(!commands.Any(c=>c.kind==CommandKind.Train),
                    faction+" AI economy: cheap infantry cannot consume the reserved first caster's mineral budget");
                Submit(sim,commands,check,faction+" caster budget reservation");
                var depositors=sim.Entities.Values.Where(e=>e.owner==0&&sim.Type(e).role=="worker").Take(2).ToArray();
                for(int i=0;i<depositors.Length;i++){depositors[i].position=new Point(10.5f,8.5f+i*2);depositors[i].cargo=8;depositors[i].cargoKind="mineral";depositors[i].depositing=true;}
                Steps(sim,8);commands=Decide(bot,sim,1);
                check(commands.Any(c=>c.kind==CommandKind.Train&&catalog[c.definition].role=="caster"),
                    faction+" AI economy: the next real mineral deliveries fund the missing caster before more infantry");
                Submit(sim,commands,check,faction+" first caster production");
                Steps(sim,catalog.Role(faction,"caster").duration+20);
                check(sim.Entities.Values.Any(e=>e.owner==0&&sim.Type(e).role=="caster"),faction+" AI economy: reserved support caster actually deploys");
            }
            {
                var sim=Fixture(catalog,faction);var expansion=Add(sim,0,"hq",new Point(34,8));
                foreach(var resource in sim.Resources.Where(r=>r.kind=="mineral"&&r.position.Distance(new Point(8,8))<18))resource.remaining=0;
                var workers=sim.Entities.Values.Where(e=>e.owner==0&&sim.Type(e).role=="worker").ToArray();
                sim.Submit(new GameCommand{player=0,kind=CommandKind.Stop,entities=workers.Select(w=>w.id).ToArray()});Warm(sim);
                sim.Players[0].minerals=0;sim.Players[0].catalyst=0;
                var commands=Decide(new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition),sim,0);
                var transfers=commands.Where(c=>c.kind==CommandKind.Gather).ToArray();
                var known=sim.View(0);
                check(transfers.Length==workers.Length&&transfers.All(c=>known.resources.Single(r=>r.id==c.target).baseId==expansion.id),
                    faction+" AI economy: depleted-base workers receive explicit transfers to a known operating expansion");
                Submit(sim,commands,check,faction+" exhausted mineral line");Steps(sim,700);
                check(sim.Players[0].minerals>0&&workers.All(w=>w.order==CommandKind.Gather),faction+" AI economy: idle transferred workers resume real income");
            }
            {
                var sim=Fixture(catalog,faction);foreach(var worker in sim.Entities.Values.Where(e=>e.owner==0&&sim.Type(e).role=="worker").ToArray())sim.Entities.Remove(worker.id);
                sim.Players[0].minerals=50;sim.Players[0].catalyst=0;
                var commands=Decide(new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition),sim,0);
                check(commands.Count(c=>c.kind==CommandKind.Train&&catalog[c.definition].role=="worker")==1,
                    faction+" AI economy: surviving headquarters can recover from losing every worker");
                Submit(sim,commands,check,faction+" worker recovery");Steps(sim,catalog.Role(faction,"worker").duration+4);
                check(sim.Entities.Values.Any(e=>e.owner==0&&sim.Type(e).role=="worker"),faction+" AI economy: replacement worker deploys after total worker loss");
            }
            {
                var sim=Fixture(catalog,faction,blockedExpansion:true);PrepareExpansion(sim);var bot=new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition);
                int scout=0,moves=0,trained=0,rejected=0;bool returned=false;
                for(int tick=0;tick<60*Simulation.TickRate;tick++)
                {
                    var view=sim.View(0);view.tick+=180*Simulation.TickRate;
                    foreach(var command in bot.Decide(view))
                    {
                        if(command.kind==CommandKind.Move&&command.entities.Any(id=>sim.Type(sim.Entities[id]).role=="worker"))
                        {moves++;if(scout==0)scout=command.entities[0];}
                        if(command.kind==CommandKind.Gather&&command.entities.Contains(scout))returned=true;
                        if(command.kind==CommandKind.Train&&catalog[command.definition].role!="worker")trained++;
                        if(!sim.Submit(command).accepted)rejected++;
                    }
                    foreach(var entity in sim.Entities.Values)entity.attackReadyTick=int.MaxValue;
                    sim.Step();
                }
                check(scout!=0&&returned,faction+" AI economy: worker scouts an unbuildable expansion then returns to useful harvesting");
                check(moves<=2&&sim.Entities.Values.Count(e=>e.owner==0&&sim.Type(e).role=="hq")==1,
                    faction+" AI economy: failed expansion is excluded instead of repeatedly moving to the same mineral cluster");
                check(trained>0,faction+" AI economy: failed expansion releases its budget and military production resumes");
                check(rejected==0,faction+" AI economy: blocked expansion recovery uses accepted commands");
            }
        }
        {
            var sim=Fixture(catalog,"industrial",blockedExpansion:true,alternateExpansion:true);PrepareExpansion(sim);
            var bot=new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition);int rejected=0;
            for(int tick=0;tick<120*Simulation.TickRate;tick++)
            {
                var view=sim.View(0);view.tick+=180*Simulation.TickRate;
                foreach(var command in bot.Decide(view))if(!sim.Submit(command).accepted)rejected++;
                foreach(var entity in sim.Entities.Values)entity.attackReadyTick=int.MaxValue;
                sim.Step();
            }
            check(sim.Entities.Values.Any(e=>e.owner==0&&e.complete&&sim.Type(e).role=="hq"&&sim.Center(e).Distance(new Point(40.5f,44.5f))<18),
                "AI economy: failed expansion reselects a different resource cluster and actually completes its headquarters");
            check(rejected==0,"AI economy: replacement expansion uses accepted build and production commands");
        }
        foreach(bool threatened in new[]{false,true})
        {
            var sim=Fixture(catalog,"industrial");PrepareExpansion(sim);var bot=new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition);
            var commands=Decide(bot,sim,180);int scout=commands.First(c=>c.kind==CommandKind.Move&&sim.Type(sim.Entities[c.entities[0]]).role=="worker").entities[0];
            Submit(sim,commands,check,"start expansion revalidation");
            if(threatened)
            {
                Add(sim,0,"scout",new Point(38.5f,14.5f));Add(sim,1,"ranged",new Point(43.5f,14.5f));Warm(sim);
            }
            else for(int i=sim.Buildings(0);i<Simulation.BuildingLimit;i++)Add(sim,0,"supply",new Point(25+(i%6)*3,35+(i/6)*3));
            sim.Players[0].minerals=100;commands=Decide(bot,sim,181);
            check(commands.Any(c=>c.kind==CommandKind.Gather&&c.entities.Contains(scout))&&!commands.Any(c=>c.kind==CommandKind.Build&&catalog[c.definition].role=="hq"),
                "AI economy: pending expansion aborts when "+(threatened?"a newly visible enemy controls the site":"the building cap leaves no headquarters slot"));
            check(commands.Any(c=>c.kind==CommandKind.Train&&catalog[c.definition].role!="worker"),
                "AI economy: invalidated expansion immediately releases military funding "+(threatened?"(enemy)":"(building cap)"));
            Submit(sim,commands,check,"expansion revalidation");
        }
        foreach(string faction in new[]{"industrial","biological"})
        {
            var sim=Fixture(catalog,faction);sim.Players[0].minerals=1000;var bot=new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition);
            var first=Decide(bot,sim,0).First(c=>c.kind==CommandKind.Build);
            check(sim.Submit(first).accepted,"AI economy: interrupted foundation is created by a real build command "+faction);
            var foundation=sim.Entities.Values.Single(e=>!e.complete);sim.Entities[first.entities[0]].health=0;Steps(sim,20);
            var commands=Decide(bot,sim,3);
            check(commands.Any(c=>c.kind==CommandKind.Repair&&c.target==foundation.id)&&!commands.Any(c=>c.kind==CommandKind.Cancel&&c.entities.Contains(foundation.id)),
                "AI economy: replaces a lost construction worker instead of cancelling the paid foundation "+faction);
            Submit(sim,commands,check,"foundation recovery");Steps(sim,catalog[foundation.definition].duration+300);
            check(foundation.complete,"AI economy: replacement builder finishes the existing structure "+faction);
        }
        foreach(string faction in new[]{"industrial","biological"})
        {
            var sim=Fixture(catalog,faction);Add(sim,0,"barracks",new Point(3,12));
            var returning=Add(sim,0,"ranged",new Point(12.5f,9.5f));returning.health=20;Warm(sim);
            sim.Players[0].minerals=300;var commands=Decide(new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition),sim,0);
            check(commands.Any(c=>c.kind==CommandKind.Repair&&c.target==returning.id),"AI economy: damaged returning troops receive worker repairs without a caster "+faction);
            Submit(sim,commands,check,"returning soldier repairs");Steps(sim,180);
            check(returning.health>=sim.Type(returning).health*.5f,"AI economy: repaired survivors recover enough health to rejoin attacks "+faction);
        }
    }

    // Peaceful macro validation uses the real clock, command validator, production,
    // construction, harvesting and terrain. Weapons/casts are disabled solely to
    // keep a fast attack from ending the match before later expansions can finish.
    public static void Expansion(Catalog catalog,MapDefinition map,MapDefinition training,Action<bool,string> check)
    {
        foreach(var arena in new[]{map,training})foreach(string faction in new[]{"industrial","biological"})
        foreach(int player in new[]{0,1})
        {
            var sim=new Simulation(arena,catalog,player==0?faction:"industrial",player==1?faction:"biological");
            var bot=new Strategist(player,AiDifficulty.Hard,catalog,arena);
            int second=-1,third=-1,scouted=0,rejected=0;string firstRejection="";
            for(int tick=0;tick<720*Simulation.TickRate&&sim.Winner==-1;tick++)
            {
                foreach(var entity in sim.Entities.Values)entity.attackReadyTick=int.MaxValue;
                foreach(var command in bot.Decide(sim.View(player)))
                {
                    if(command.kind==CommandKind.Cast)continue;
                    var result=sim.Submit(command);
                    bool economic=command.kind==CommandKind.Build||command.kind==CommandKind.Train||command.kind==CommandKind.Gather
                        ||command.kind==CommandKind.Repair||command.kind==CommandKind.Research;
                    if(!result.accepted&&economic){rejected++;if(firstRejection=="")firstRejection=command.kind+":"+result.message;}
                    if(result.accepted&&command.kind==CommandKind.Move&&command.entities.Any(id=>sim.Entities.TryGetValue(id,out var e)&&sim.Type(e).role=="worker"))scouted++;
                }
                sim.Step();
                int bases=sim.Entities.Values.Count(e=>e.owner==player&&e.complete&&sim.Type(e).role=="hq");
                if(bases>=2&&second<0)second=sim.Tick/Simulation.TickRate;
                if(bases>=3&&third<0)third=sim.Tick/Simulation.TickRate;
                if(sim.Tick%Simulation.TickRate==0&&sim.Tick/Simulation.TickRate%120==0)
                    Console.WriteLine($"AI_MACRO {arena.id}/{faction}/p{player} t={sim.Tick/Simulation.TickRate}s bases={bases} workers={sim.Entities.Values.Count(e=>e.owner==player&&sim.Type(e).role=="worker")} army={sim.Entities.Values.Count(e=>e.owner==player&&!sim.Type(e).building&&sim.Type(e).role!="worker")} M={sim.Players[player].minerals} G={sim.Players[player].catalyst} rejected={rejected}");
            }
            string label=$"{arena.id}/{faction}/p{player}";
            check(second>=0&&second<=420,"AI macro: completed second resource base by 7 minutes "+label+" actual="+second);
            check(third>=0&&third<=720,"AI macro: completed third resource base by 12 minutes "+label+" actual="+third);
            check(scouted>0,"AI macro: expansion construction follows real worker scouting "+label);
            check(sim.HasRole(player,"tech")&&sim.HasRole(player,"academy"),"AI macro: reaches advanced production while developing its economy "+label);
            check(rejected==0,"AI macro: economic orders accepted "+label+" rejected="+rejected+" first="+firstRejection);
        }
    }

    private static GameCommand[] Decide(Strategist bot,Simulation sim,int seconds)
    {var view=sim.View(0);view.tick=seconds*Simulation.TickRate;return bot.Decide(view).ToArray();}
    private static void Submit(Simulation sim,IEnumerable<GameCommand> commands,Action<bool,string> check,string label)
    {
        var failures=new List<string>();foreach(var command in commands){var result=sim.Submit(command);if(!result.accepted)failures.Add(command.kind+":"+result.message);}
        check(failures.Count==0,"AI economy commands accepted ("+label+"): "+string.Join("; ",failures));
    }
    private static void Steps(Simulation sim,int count){for(int i=0;i<count;i++)sim.Step();}
    private static void Warm(Simulation sim)=>Steps(sim,4);
    private static EntityState Headquarters(Simulation sim)=>sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="hq");
    private static EntityState Add(Simulation sim,int owner,string role,Point point)
    {
        var type=sim.Catalog.Role(sim.Players[owner].faction,role);int id=Math.Max(1000,sim.Entities.Keys.Max()+1);
        var entity=new EntityState{id=id,owner=owner,definition=type.id,position=point,health=type.health,energy=type.energyMax,
            order=CommandKind.Hold,attackReadyTick=int.MaxValue};sim.Entities.Add(id,entity);if(type.building)sim.Map.Occupy(point,type.footprint,id);return entity;
    }
    private static bool Overlaps(Point unit,float radius,Point origin,int size)
    {var nearest=new Point(Math.Max(origin.x,Math.Min(origin.x+size,unit.x)),Math.Max(origin.y,Math.Min(origin.y+size,unit.y)));return unit.Distance(nearest)<radius;}
    private static bool RectanglesOverlap(Point a,int sizeA,Point b,int sizeB)=>a.x<b.x+sizeB&&a.x+sizeA>b.x&&a.y<b.y+sizeB&&a.y+sizeA>b.y;
    private static void PrepareExpansion(Simulation sim)
    {
        Add(sim,0,"barracks",new Point(3,12));Add(sim,0,"extractor",new Point(9,19));
        Add(sim,0,"supply",new Point(3,4));Add(sim,0,"supply",new Point(13,9));Add(sim,0,"supply",new Point(17,9));
        for(int i=0;i<7;i++)Add(sim,0,"worker",new Point(16.5f+i,11.5f));
        Warm(sim);sim.Players[0].minerals=100;sim.Players[0].catalyst=0;
    }
    private static Simulation Fixture(Catalog catalog,string faction,bool blockedExpansion=false,bool alternateExpansion=false)
    {
        var map=new MapDefinition{id="ai-economy",title="AI economy",width=80,height=60,
            spawns=new[]{new SpawnDefinition{player=0,x=6,y=6},new SpawnDefinition{player=1,x=70,y=50}},
            resources=new[]{new ResourceDefinition{id="home0",x=12,y=14,amount=6000},new ResourceDefinition{id="home1",x=15,y=14,amount=6000},
                new ResourceDefinition{id="home2",x=15,y=17,amount=6000},new ResourceDefinition{id="home3",x=12,y=17,amount=6000},
                new ResourceDefinition{id="gas0",kind="catalyst",x=10,y=20},new ResourceDefinition{id="exp0",x=40,y=14,amount=6000},
                new ResourceDefinition{id="exp1",x=40,y=17,amount=6000},new ResourceDefinition{id="expgas",kind="catalyst",x=40,y=21},
                new ResourceDefinition{id="enemy",x=68,y=46,amount=6000},new ResourceDefinition{id="enemyGas",kind="catalyst",x=75,y=46}}};
        if(blockedExpansion)map.terrain=new[]{new TerrainRegion{x=25,y=0,width=32,height=40,walkable=true,buildable=false}};
        if(alternateExpansion)map.resources=map.resources.Concat(new[]{new ResourceDefinition{id="alternative",x=40,y=44,amount=6000}}).ToArray();
        return new Simulation(map,catalog,faction,faction=="industrial"?"biological":"industrial");
    }
}
