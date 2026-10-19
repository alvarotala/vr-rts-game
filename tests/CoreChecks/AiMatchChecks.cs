using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Frontier.Core;

// Standalone benchmark entry point. The comparison bot is injected by check-ai.sh,
// so historical strategy code never becomes part of the game or normal core build.
static class AiMatchChecks
{
    public delegate Func<PlayerView,IEnumerable<GameCommand>> BotFactory(int player,AiDifficulty difficulty,Catalog catalog,MapDefinition map);
    private static readonly JsonSerializerOptions Json=new(){IncludeFields=true,WriteIndented=true};
    private static readonly JsonSerializerOptions LineJson=new(){IncludeFields=true};
    private static readonly string[] Factions={"industrial","biological"};

    public static void Run(Catalog catalog,MapDefinition[] maps,string root,string[] args,BotFactory legacy)
    {
        string Option(string name,string fallback)=>args.FirstOrDefault(a=>a.StartsWith("--"+name+"="))?.Substring(name.Length+3)??fallback;
        int limitSeconds=int.Parse(Option("seconds","1800")),wallSeconds=int.Parse(Option("wall-seconds","180"));
        int developmentSeconds=int.Parse(Option("development-seconds","720"));
        bool developmentOnly=args.Contains("--development-only"),development=developmentOnly||args.Contains("--development");
        int start=int.Parse(Option("start","0")),limit=int.Parse(Option("limit","48"));
        if(limitSeconds<1||wallSeconds<1||developmentSeconds<1||start<0||limit<1)throw new ArgumentException("Invalid benchmark limit.");
        string mode=Option("difficulty","Hard"),legacyMode=Option("legacy-difficulty","same"),opponent=Option("opponent","legacy").ToLowerInvariant();
        if(opponent!="legacy"&&opponent!="current")throw new ArgumentException("Opponent must be legacy or current.");
        BotFactory comparator=opponent=="current"?Current:legacy;
        var levels=mode.Equals("all",StringComparison.OrdinalIgnoreCase)?Enum.GetValues<AiDifficulty>():new[]{Enum.Parse<AiDifficulty>(mode,true)};
        string mapFilter=Option("map","all"),pairFilter=Option("pair","all"),sideFilter=Option("side","both");
        var arenas=maps.Where(m=>mapFilter=="all"||m.id==mapFilter).OrderBy(m=>m.width).ToArray();
        var sides=sideFilter=="both"?new[]{0,1}:new[]{int.Parse(sideFilter)};
        if(arenas.Length==0||sides.Any(side=>side<0||side>1))throw new ArgumentException("Unknown map or side.");
        string output=Path.GetFullPath(Option("output",Path.Combine(root,"TestResults/ai-matches.json")));
        string baselineSource=Option("baseline-source",Path.Combine(root,"TestResults/ai-before/LegacyStrategist.cs"));
        string dataDirectory=Option("data-dir",Path.Combine(root,"Assets/Frontier/Resources"));
        var cases=new List<Case>();
        // The first six cases with --difficulty=all cover all levels and both maps
        // in the industrial mirror. Broader runs then swap sides and faction pairs.
        foreach(string currentFaction in Factions)foreach(string oldFaction in Factions)
        {
            if(pairFilter!="all"&&!(pairFilter=="mirrors"&&currentFaction==oldFaction)&&pairFilter!=currentFaction+"-"+oldFaction)continue;
            foreach(int side in sides)foreach(var map in arenas)foreach(var level in levels)
                cases.Add(new Case{index=cases.Count,map=map,difficulty=level,legacyDifficulty=legacyMode=="same"?level:Enum.Parse<AiDifficulty>(legacyMode,true),
                    currentFaction=currentFaction,legacyFaction=oldFaction,currentSide=side});
        }
        var selected=developmentOnly?Array.Empty<Case>():cases.Skip(start).Take(limit).ToArray();
        if(selected.Length==0&&!developmentOnly)throw new ArgumentException("No matching benchmark cases.");
        var suite=new Suite{opponentStrategy=opponent,baselineSource=baselineSource,sourceSnapshot=Option("source-snapshot","live workspace"),
            baselineSha256=Hash(File.ReadAllText(Option("baseline-copy",baselineSource))),
            currentCoreSha256=Hash(string.Join("\n",Directory.GetFiles(Option("current-source-dir",Path.Combine(root,"Assets/Frontier/Core")),"*.cs").OrderBy(p=>p)
                .Select(path=>Path.GetFileName(path)+"\n"+File.ReadAllText(path)))),
            catalogSha256=Hash(File.ReadAllText(Path.Combine(dataDirectory,"catalog.json"))),
            benchmarkSha256=Hash(File.ReadAllText(Option("benchmark-source",Path.Combine(root,"tests/CoreChecks/AiMatchChecks.cs")))),
            simulatedLimitSeconds=limitSeconds,wallLimitSeconds=wallSeconds,totalSelected=selected.Length,
            informationBoundaryChecks=InformationBoundary(catalog,arenas,levels,Current)};
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        foreach(var test in selected)
        {
            var result=Fight(catalog,test,limitSeconds,wallSeconds,comparator);result.opponentStrategy=opponent;
            suite.matches.Add(result);
            File.WriteAllText(output,JsonSerializer.Serialize(suite,Json)+"\n");
            Console.WriteLine("AI_MATCH "+JsonSerializer.Serialize(result,LineJson));
        }
        if(development)
        {
            // Isolate economic growth from winning early. Only damage changes in
            // this synthetic fixture: economy, costs, timing, movement and map stay
            // intact. Its outcome is never counted as a competitive win.
            var definitions=JsonSerializer.Deserialize<CatalogDefinition>(JsonSerializer.Serialize(catalog.Definition,LineJson),LineJson)!;
            foreach(var type in definitions.archetypes)type.damage=0;
            foreach(var ability in definitions.abilities)if(ability.effect=="damage")ability.amount=.0001f;
            var peaceful=new Catalog(definitions);suite.developmentCatalogSha256=Hash(JsonSerializer.Serialize(definitions,LineJson));
            foreach(var map in arenas)foreach(var level in levels)foreach(string faction in Factions)
            {
                var test=new Case{index=suite.development.Count,map=map,difficulty=level,legacyDifficulty=level,currentSide=0,currentFaction=faction,legacyFaction=faction=="industrial"?"biological":"industrial"};
                var result=Fight(peaceful,test,developmentSeconds,wallSeconds,(_,_,_,_)=>_=>Array.Empty<GameCommand>());
                result.synthetic="Economy-only: all weapon damage disabled; damage abilities 0.0001. Passive opponent. Original costs, income rules, construction/training speed and authored map.";
                result.opponentStrategy="passive";result.legacyDifficulty="passive";result.outcome=result.simulatedSeconds>=developmentSeconds?"development-complete":"development-incomplete";
                if(result.outcome=="development-complete")result.timeoutReason=null;
                suite.development.Add(result);File.WriteAllText(output,JsonSerializer.Serialize(suite,Json)+"\n");
                Console.WriteLine("AI_DEVELOPMENT "+JsonSerializer.Serialize(result,LineJson));
            }
        }
        Console.WriteLine("AI_SUMMARY "+JsonSerializer.Serialize(new{suite.opponentStrategy,matches=suite.matches.Count,currentWins=suite.matches.Count(m=>m.outcome=="current"),
            legacyWins=suite.matches.Count(m=>m.outcome=="legacy"),timeouts=suite.matches.Count(m=>m.outcome=="timeout"),
            developmentCases=suite.development.Count,wallSeconds=suite.matches.Concat(suite.development).Sum(m=>m.wallSeconds),suite.informationBoundaryChecks,output},LineJson));
    }

    private static Func<PlayerView,IEnumerable<GameCommand>> Current(int player,AiDifficulty level,Catalog catalog,MapDefinition map)
    {var bot=new Strategist(player,level,catalog,map);return bot.Decide;}
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static Point Center(EntityState entity,Archetype type)=>type.building?new Point(entity.position.x+type.footprint*.5f,entity.position.y+type.footprint*.5f):entity.position;

    private static int InformationBoundary(Catalog catalog,MapDefinition[] maps,AiDifficulty[] levels,BotFactory factory)
    {
        int checks=0;
        foreach(var map in maps)foreach(var level in levels)foreach(string faction in Factions)
        {
            var ordinary=new Simulation(map,catalog,faction);var hidden=new Simulation(map,catalog,faction);
            hidden.Players[1].minerals=50000;hidden.Players[1].catalyst=50000;
            foreach(var entity in hidden.Entities.Values.Where(e=>e.owner==1))
            {entity.health+=700;entity.energy=100;entity.order=CommandKind.AttackMove;entity.destination=new Point(5,5);}
            var a=ordinary.View(0);var b=hidden.View(0);
            if(JsonSerializer.Serialize(a,LineJson)!=JsonSerializer.Serialize(b,LineJson))throw new Exception("Hidden-state fixture leaks through PlayerView.");
            var first=factory(0,level,catalog,map)(a).ToArray();var second=factory(0,level,catalog,map)(b).ToArray();
            if(JsonSerializer.Serialize(first,LineJson)!=JsonSerializer.Serialize(second,LineJson))throw new Exception("AI decisions depend on hidden enemy state.");
            checks++;
        }
        return checks;
    }

    private static Match Fight(Catalog catalog,Case test,int limitSeconds,int wallLimit,BotFactory legacy)
    {
        string faction0=test.currentSide==0?test.currentFaction:test.legacyFaction;
        string faction1=test.currentSide==1?test.currentFaction:test.legacyFaction;
        var sim=new Simulation(test.map,catalog,faction0,faction1);
        var bots=new Func<PlayerView,IEnumerable<GameCommand>>[2];
        bots[test.currentSide]=Current(test.currentSide,test.difficulty,catalog,test.map);
        bots[1-test.currentSide]=legacy(1-test.currentSide,test.legacyDifficulty,catalog,test.map);
        var stats=new[]{new PlayerStats{side=0,faction=faction0},new PlayerStats{side=1,faction=faction1}};
        var seenCasts=new[]{new HashSet<int>(),new HashSet<int>()};
        var homes=test.map.spawns.OrderBy(s=>s.player).Select(s=>new Point(s.x+2,s.y+2)).ToArray();
        var watch=Stopwatch.StartNew();var previous=new List<HealthState>();bool wallTimeout=false;
        for(int tick=0;tick<limitSeconds*Simulation.TickRate&&sim.Winner==-1;tick++)
        {
            if(tick%Simulation.TickRate==0)
            {
                if(watch.Elapsed.TotalSeconds>wallLimit){wallTimeout=true;break;}
                // Both bots see the same pre-order tick; command submission order
                // alternates each second to avoid a systematic first-controller edge.
                var views=new[]{sim.View(0),sim.View(1)};
                for(int n=0;n<2;n++)
                {
                    int player=(n+tick/Simulation.TickRate)%2;var info=stats[player];var view=views[player];
                    foreach(var effect in view.abilityEvents)if(effect.owner==player&&seenCasts[player].Add(effect.id))info.castsExecuted++;
                    if(info.firstEnemyBaseSeenSeconds<0&&view.entities.Any(e=>e.owner!=player&&catalog[e.definition].role=="hq"))info.firstEnemyBaseSeenSeconds=tick/Simulation.TickRate;
                    foreach(var command in bots[player](view))
                    {
                        if(command.player!=player)throw new Exception("AI command attempted to control its opponent.");
                        var result=sim.Submit(command);
                        if(!result.accepted)
                        {
                            info.rejected++;info.rejections[result.message]=info.rejections.GetValueOrDefault(result.message)+1;
                            if(command.kind==CommandKind.Build)info.rejectedBuilds++;
                            continue;
                        }
                        info.accepted++;
                        if(command.kind==CommandKind.Build)
                        {
                            info.buildingsOrdered++;
                            string role=catalog[command.definition].role;info.buildingsOrderedByRole[role]=info.buildingsOrderedByRole.GetValueOrDefault(role)+1;
                            if(catalog[command.definition].role=="hq")
                            {
                                int bases=sim.Entities.Values.Count(e=>e.owner==player&&catalog[e.definition].role=="hq");
                                if(bases>=2&&info.secondHeadquartersOrderedSeconds<0)info.secondHeadquartersOrderedSeconds=tick/Simulation.TickRate;
                                if(bases>=3&&info.thirdHeadquartersOrderedSeconds<0)info.thirdHeadquartersOrderedSeconds=tick/Simulation.TickRate;
                            }
                        }
                        if(command.kind==CommandKind.Train)
                        {info.unitsOrdered++;string role=catalog[command.definition].role;info.unitsOrderedByRole[role]=info.unitsOrderedByRole.GetValueOrDefault(role)+1;}
                        if(command.kind==CommandKind.Cast)info.castsOrdered++;
                        if(command.kind==CommandKind.Move&&info.firstScoutOrderSeconds<0&&command.position.Distance(homes[player])>18
                            &&command.entities.Any(id=>sim.Entities.TryGetValue(id,out var e)&&catalog[e.definition].role=="scout"))info.firstScoutOrderSeconds=tick/Simulation.TickRate;
                        bool offense=command.kind==CommandKind.AttackMove&&command.position.Distance(homes[player])>16
                            &&command.position.Distance(homes[1-player])<command.position.Distance(homes[player]);
                        if(offense)
                        {
                            if(info.firstOffensiveOrderSeconds<0)info.firstOffensiveOrderSeconds=tick/Simulation.TickRate;
                            info.largestOffensiveOrder=Math.Max(info.largestOffensiveOrder,command.entities.Length);
                        }
                    }
                }
                for(int player=0;player<2;player++)Sample(sim,catalog,stats[player],tick/Simulation.TickRate,homes[player]);
                if(sim.Players.Any(p=>p.minerals<0||p.catalyst<0)||Enumerable.Range(0,2).Any(p=>sim.Units(p)+sim.Reserved(p)>Simulation.UnitLimit))
                    throw new Exception("AI violated resource or population limits.");
            }
            previous.Clear();
            foreach(var entity in sim.Entities.Values)
                previous.Add(new HealthState{id=entity.id,owner=entity.owner,health=Math.Max(0,entity.health),role=catalog[entity.definition].role,building=catalog[entity.definition].building,
                    cargo=entity.cargo,cargoKind=entity.cargoKind});
            sim.Step();
            foreach(var old in previous)
            {
                bool alive=sim.Entities.TryGetValue(old.id,out var now);
                if(alive&&old.role=="worker")
                {
                    int gathered=now!.cargo-old.cargo;
                    if(gathered>0){if(now.cargoKind=="catalyst")stats[old.owner].catalystGathered+=gathered;else stats[old.owner].mineralsGathered+=gathered;}
                    else if(gathered<0){if(old.cargoKind=="catalyst")stats[old.owner].catalystDelivered-=gathered;else stats[old.owner].mineralsDelivered-=gathered;}
                }
                float damage=Math.Max(0,old.health-(alive?Math.Max(0,now!.health):0));
                if(damage<=0)continue;
                var attacker=stats[1-old.owner];attacker.netHpDamage+=damage;
                if(attacker.firstDamageSeconds<0)attacker.firstDamageSeconds=sim.Tick/(float)Simulation.TickRate;
                if(old.role=="hq"&&attacker.firstHeadquartersDamageSeconds<0)attacker.firstHeadquartersDamageSeconds=sim.Tick/(float)Simulation.TickRate;
                if(!alive){if(old.building)attacker.structuresDestroyed++;else attacker.unitsDestroyed++;}
            }
        }
        for(int player=0;player<2;player++)
        {
            Sample(sim,catalog,stats[player],sim.Tick/Simulation.TickRate,homes[player],true);
            foreach(var effect in sim.View(player).abilityEvents)if(effect.owner==player&&seenCasts[player].Add(effect.id))stats[player].castsExecuted++;
        }
        return new Match{index=test.index,map=test.map.id,mapSha256=Hash(JsonSerializer.Serialize(test.map,LineJson)),difficulty=test.difficulty.ToString(),legacyDifficulty=test.legacyDifficulty.ToString(),
            currentSide=test.currentSide,currentFaction=test.currentFaction,legacyFaction=test.legacyFaction,winner=sim.Winner,
            outcome=sim.Winner<0?"timeout":sim.Winner==test.currentSide?"current":"legacy",timeoutReason=sim.Winner<0?(wallTimeout?"wall-clock limit":"simulation limit"):null,
            simulatedSeconds=sim.Tick/(float)Simulation.TickRate,wallSeconds=watch.Elapsed.TotalSeconds,current=stats[test.currentSide],legacy=stats[1-test.currentSide]};
    }

    private static void Sample(Simulation sim,Catalog catalog,PlayerStats stats,int seconds,Point home,bool final=false)
    {
        var own=sim.Entities.Values.Where(e=>e.owner==stats.side).ToArray();
        int workers=own.Count(e=>catalog[e.definition].role=="worker"),army=own.Count(e=>!catalog[e.definition].building&&catalog[e.definition].role!="worker");
        int queuedArmy=own.Sum(e=>e.queue.Count(q=>!q.research&&catalog[q.definition].role!="worker"));
        int queuedWorkers=own.Sum(e=>e.queue.Count(q=>!q.research&&catalog[q.definition].role=="worker"));
        var producers=own.Where(e=>e.complete&&catalog[e.definition].role!="hq"&&catalog.CanProduceUnits(e.definition)).ToArray();
        int busy=producers.Count(e=>e.queue.Count>0);
        int bases=own.Count(e=>e.complete&&catalog[e.definition].role=="hq");
        stats.peakCompletedBases=Math.Max(stats.peakCompletedBases,bases);
        if(bases>=2&&stats.secondHeadquartersCompletedSeconds<0)stats.secondHeadquartersCompletedSeconds=seconds;
        if(bases>=3&&stats.thirdHeadquartersCompletedSeconds<0)stats.thirdHeadquartersCompletedSeconds=seconds;
        if(stats.firstScoutLeftHomeSeconds<0&&own.Any(e=>catalog[e.definition].role=="scout"&&e.position.Distance(home)>18))stats.firstScoutLeftHomeSeconds=seconds;
        stats.peakArmy=Math.Max(stats.peakArmy,army+queuedArmy);stats.peakWorkers=Math.Max(stats.peakWorkers,workers);
        if(!final)
        {
            stats.producerSeconds+=producers.Length;stats.busyProducerSeconds+=busy;
            stats.idleWorkerSeconds+=own.Count(e=>catalog[e.definition].role=="worker"&&e.order==CommandKind.Stop);
            if(sim.Capacity(stats.side)<Simulation.UnitLimit&&sim.Units(stats.side)+sim.Reserved(stats.side)>=sim.Capacity(stats.side))stats.supplyBlockedSeconds++;
        }
        if(final||seconds%60==0)
        {
            var sample=new SamplePoint{seconds=seconds,workers=workers,queuedWorkers=queuedWorkers,army=army,queuedArmy=queuedArmy,
                minerals=sim.Players[stats.side].minerals,catalyst=sim.Players[stats.side].catalyst,supply=sim.Units(stats.side)+sim.Reserved(stats.side),capacity=sim.Capacity(stats.side),
                bases=bases,militaryProducers=producers.Length,busyMilitaryProducers=busy,
                mineralsDelivered=stats.mineralsDelivered,catalystDelivered=stats.catalystDelivered,
                armyByRole=own.Where(e=>!catalog[e.definition].building&&catalog[e.definition].role!="worker").GroupBy(e=>catalog[e.definition].role).ToDictionary(g=>g.Key,g=>g.Count()),
                queuedUnitsByRole=own.SelectMany(e=>e.queue).Where(q=>!q.research).GroupBy(q=>catalog[q.definition].role).ToDictionary(g=>g.Key,g=>g.Count()),
                buildings=own.Count(e=>catalog[e.definition].building),armyHealth=own.Where(e=>!catalog[e.definition].building&&catalog[e.definition].role!="worker").Sum(e=>Math.Max(0,e.health))};
            if(stats.samples.LastOrDefault()?.seconds==seconds)stats.samples[^1]=sample;else stats.samples.Add(sample);
        }
    }

    private sealed class Case
    {
        public int index,currentSide;public MapDefinition map=null!;public AiDifficulty difficulty,legacyDifficulty;
        public string currentFaction="",legacyFaction="";
    }
    private struct HealthState {public int id,owner,cargo;public float health;public string role,cargoKind;public bool building;}
    private sealed class Suite
    {
        public string opponentStrategy="legacy",baselineSource="",sourceSnapshot="",baselineSha256="",currentCoreSha256="",catalogSha256="",benchmarkSha256="",developmentCatalogSha256="";
        public int simulatedLimitSeconds,wallLimitSeconds,totalSelected,informationBoundaryChecks;
        public List<Match> matches=new();
        public List<Match> development=new();
    }
    private sealed class Match
    {
        // Legacy-named fields retain the comparator slot for evidence compatibility;
        // opponentStrategy explicitly identifies whether it uses old or current code.
        public int index,currentSide,winner;public string opponentStrategy="legacy",map="",mapSha256="",difficulty="",legacyDifficulty="",currentFaction="",legacyFaction="",outcome="";
        public string timeoutReason,synthetic;public float simulatedSeconds;public double wallSeconds;public PlayerStats current=null!,legacy=null!;
    }
    private sealed class PlayerStats
    {
        public int side,accepted,rejected,rejectedBuilds,unitsOrdered,buildingsOrdered,castsOrdered,castsExecuted,peakArmy,peakWorkers,largestOffensiveOrder;
        public int structuresDestroyed,unitsDestroyed,producerSeconds,busyProducerSeconds,idleWorkerSeconds,supplyBlockedSeconds;
        public int firstScoutOrderSeconds=-1,firstScoutLeftHomeSeconds=-1,secondHeadquartersOrderedSeconds=-1,thirdHeadquartersOrderedSeconds=-1;
        public int secondHeadquartersCompletedSeconds=-1,thirdHeadquartersCompletedSeconds=-1,peakCompletedBases,mineralsGathered,catalystGathered,mineralsDelivered,catalystDelivered;
        public int firstOffensiveOrderSeconds=-1,firstEnemyBaseSeenSeconds=-1;public float firstDamageSeconds=-1,firstHeadquartersDamageSeconds=-1,netHpDamage;
        public string faction="";public Dictionary<string,int> rejections=new(),unitsOrderedByRole=new(),buildingsOrderedByRole=new();public List<SamplePoint> samples=new();
    }
    private sealed class SamplePoint
    {
        public int seconds,workers,queuedWorkers,army,queuedArmy,minerals,catalyst,supply,capacity,bases,militaryProducers,busyMilitaryProducers,buildings;
        public int mineralsDelivered,catalystDelivered;
        public Dictionary<string,int> armyByRole=new(),queuedUnitsByRole=new();
        public float armyHealth;
    }
}
