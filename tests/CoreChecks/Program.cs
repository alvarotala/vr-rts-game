using System.Diagnostics;
using System.Text.Json;
using Frontier.Core;

var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = false };
string root = args.FirstOrDefault(a => a.StartsWith("--root="))?.Substring(7) ?? Directory.GetCurrentDirectory();
T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(root, path)), json);
var catalog = new Catalog(Read<CatalogDefinition>("Assets/Frontier/Resources/catalog.json"));
var map = Read<MapDefinition>("Assets/Frontier/Resources/Maps/rift.json");
var training = Read<MapDefinition>("Assets/Frontier/Resources/Maps/training.json");
var desert = Read<MapDefinition>("Assets/Frontier/Resources/Maps/desert.json");
int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
void Throws(Action action, string name) { try { action(); } catch (ArgumentException) { Check(true, name); return; } Check(false, name); }
Simulation New(string a = "industrial", string b = "biological") => new Simulation(map, catalog, a, b);
GameCommand Cmd(CommandKind kind, int id, int player = 0, string definition = null, Point position = default, int target = 0) => new GameCommand { kind = kind, entities = new[] { id }, player = player, definition = definition, position = position, target = target };

if(args.Contains("--maps-only"))
{
    ResourceLayoutChecks.Run(catalog,new[]{map,training},Check);
    DesertMapChecks.Run(catalog,desert,map,Check);
    Console.WriteLine($"{checks} map checks passed.");
    return;
}
if(args.Contains("--recording-only"))
{
    MatchRecordingChecks.Run(catalog,map,Check);
    Console.WriteLine($"{checks} match recording checks passed.");
    return;
}
if(args.Contains("--match-report-only"))
{
    MatchReportChecks.Run(catalog,map,Check);
    Console.WriteLine($"{checks} match report checks passed.");
    return;
}
if(args.Contains("--desert-map-only"))
{
    DesertMapChecks.Run(catalog,desert,map,Check);
    Console.WriteLine($"{checks} desert map checks passed.");
    return;
}
if(args.Contains("--construction-clearance-only"))
{
    ConstructionClearanceChecks.Run(catalog, Check);
    Console.WriteLine($"{checks} construction clearance checks passed.");
    return;
}
if(args.Contains("--deaths-only"))
{
    DeathEventChecks.Run(catalog, map, Check);
    Console.WriteLine($"{checks} death event checks passed.");
    return;
}
if(args.Contains("--group-production-only"))
{
    GroupProductionChecks.Run(catalog,map,Check);
    Console.WriteLine($"{checks} group production checks passed.");
    return;
}
if(args.Contains("--faction-parity-only"))
{
    FactionParityChecks.Run(catalog,new[]{map,training},Check);
    Console.WriteLine($"{checks} faction parity checks passed.");
    return;
}
if(args.Contains("--ai-expansion-only"))
{
    AiEconomyChecks.Expansion(catalog,map,training,Check);
    Console.WriteLine($"{checks} AI expansion checks passed.");
    return;
}
if(args.Contains("--ai-only"))
{
    AiEconomyChecks.Run(catalog,map,training,Check);
    AiTacticsChecks.Run(catalog,Check);
    AiTacticsChecks.Scouting(catalog,map,training,Check);
    DifficultyChecks.Run(catalog,map,training,Check);
    if(args.Contains("--practice"))DifficultyChecks.Practice(catalog,new[]{map,training},Check,root);
    Console.WriteLine($"{checks} AI checks passed.");
    return;
}
if(args.Contains("--caster-only"))
{
    CasterChecks.Run(catalog,map,Check);
    Console.WriteLine($"{checks} caster checks passed.");
    return;
}
if(args.Contains("--combat-only"))
{
    CombatBehaviorChecks.Run(catalog,Check);
    CombatMotionChecks.Run(catalog,Check);
    Console.WriteLine($"{checks} combat checks passed.");
    return;
}
if(args.Contains("--harvesting-only"))
{
    HarvestingChecks.Run(catalog,map,Check);
    HarvestingChecks.Depletion(catalog,new[]{map,training},Check);
    Console.WriteLine($"{checks} harvesting checks passed.");
    return;
}

FactionParityChecks.Run(catalog,new[]{map,training},Check);
DesertMapChecks.Run(catalog,desert,map,Check);
Check(new GridMap(map).Count == 16384 && new GridMap(training).Count == 2304, "both authored maps validate");
ResourceLayoutChecks.Run(catalog, new[]{map, training}, Check);
var roundtrip = JsonSerializer.Deserialize<MapDefinition>(JsonSerializer.Serialize(map, json), json);
Check(new GridMap(roundtrip).Definition.resources.Length == map.resources.Length, "map roundtrip preserves resources");
Check(JsonSerializer.Serialize(roundtrip,json)==JsonSerializer.Serialize(map,json),"map roundtrip preserves every field");
var invalidMap=JsonSerializer.Deserialize<MapDefinition>(JsonSerializer.Serialize(map,json),json);
foreach(var deposit in invalidMap.resources)deposit.kind="catalyst";
Throws(()=>new GridMap(invalidMap),"map without mineral rejects before match initialization");
var invalidCatalog=Read<CatalogDefinition>("Assets/Frontier/Resources/catalog.json");invalidCatalog.upgrades[0].minerals=-1;
Throws(()=>new Catalog(invalidCatalog),"negative upgrade cost rejected");
invalidCatalog=Read<CatalogDefinition>("Assets/Frontier/Resources/catalog.json");invalidCatalog.archetypes.First(t=>!t.building).producer="missing";
Throws(()=>new Catalog(invalidCatalog),"missing producer reference rejected");
roundtrip.schemaVersion = 99; Throws(() => new GridMap(roundtrip), "unknown schema rejected");
roundtrip.schemaVersion = 1; roundtrip.resources[0].id = roundtrip.resources[1].id; Throws(() => new GridMap(roundtrip), "duplicate resource IDs rejected");
var s = New();
Check(s.Units(0) == 6 && s.Units(1) == 6 && s.Buildings(0) == 1, "symmetric initial economy");
Check(s.View(0).entities.All(e => e.owner == 0), "fog hides enemy starting army");
var hq = s.Entities.Values.First(e => e.owner == 0 && s.Type(e).role == "hq");
var worker = s.Entities.Values.First(e => e.owner == 0 && s.Type(e).role == "worker");
var enemy = s.Entities.Values.First(e => e.owner == 1 && !s.Type(e).building);
int cash = s.Players[0].minerals;
Check(!s.Submit(Cmd(CommandKind.Move, enemy.id, position: new Point(20,20))).accepted, "cannot control enemy units");
Check(!s.Submit(Cmd(CommandKind.Attack, worker.id, target: enemy.id)).accepted, "cannot attack hidden enemies");
Check(!s.Submit(Cmd(CommandKind.Build, worker.id, definition: "biological.hq", position: new Point(20,20))).accepted && cash == s.Players[0].minerals, "invalid faction build does not spend resources");
Check(s.Submit(Cmd(CommandKind.Train, hq.id, definition: "industrial.worker")).accepted && s.Players[0].minerals == cash - 50, "production spends resources once");
Check(s.Submit(Cmd(CommandKind.Cancel, hq.id)).accepted && s.Players[0].minerals == cash, "queued production cancellation refunds");
var duplicate = Cmd(CommandKind.Stop, worker.id); duplicate.sequence = 1;
Check(s.Submit(duplicate).accepted && !s.Submit(duplicate).accepted, "duplicate sequence rejected");
s.Paused = true; s.Step(); Check(s.Tick == 0, "pause freezes simulation"); s.Paused = false;
for(int i=0;i<1200;i++)s.Step();
Check(s.Players[0].minerals > cash && s.Resources.Sum(r=>r.remaining) < map.resources.Sum(r=>r.amount), "workers extract finite resources and deposit income");
var grid = new GridMap(map); var pathfinder = new Pathfinder(grid);
var route = pathfinder.Find(new Point(16.5f,16.5f), new Point(110.5f,110.5f));
Check(route != null && route.Count > 0, "pathfinder connects opposing bases across ramps");
Check(!grid.Traversable(grid.Index(51,55),grid.Index(52,55)), "cliff cannot be crossed without ramp");
Check(grid.Traversable(grid.Index(49,40),grid.Index(50,40)), "ramp connects elevations");
s = New(); s.Players[0].minerals = 10000;
hq = s.Entities.Values.First(e=>e.owner==0 && s.Type(e).role=="hq");
for(int i=0;i<4;i++)Check(s.Submit(Cmd(CommandKind.Train,hq.id,definition:"industrial.worker")).accepted, "reserve available supply " + i);
Check(!s.Submit(Cmd(CommandKind.Train,hq.id,definition:"industrial.worker")).accepted, "queued units reserve supply");
for(int i=0;i<1100;i++)s.Step();
Check(s.Units(0)==10 && s.Reserved(0)==0, "production finishes and units exit");
Check(s.Submit(new GameCommand{kind=CommandKind.Surrender,player=1}).accepted && s.Winner==0,"surrender ends match");

// Building placement, construction, upgrades, fog memory and blocked production exercise complete behavior.
foreach(string faction in new[]{"industrial","biological"})
{
    s=New(faction);worker=s.Entities.Values.First(e=>e.owner==0&&s.Type(e).role=="worker");
    s.Players[0].minerals=10000;s.Players[0].catalyst=1000;
    var supply=catalog.Role(faction,"supply");
    int initial=s.Players[0].minerals;
    Check(s.Submit(Cmd(CommandKind.Build,worker.id,definition:supply.id,position:new Point(18,14))).accepted,"valid foundation "+faction);
    var foundation=s.Entities.Values.First(e=>e.owner==0&&e.definition==supply.id);
    Check(!s.Submit(Cmd(CommandKind.Build,worker.id,definition:supply.id,position:new Point(18,14))).accepted&&s.Players[0].minerals==initial-supply.minerals,"occupied footprint rejects without charge "+faction);
    for(int i=0;i<900;i++)s.Step();
    Check(foundation.complete&&s.Capacity(0)==20,"construction grants supply "+faction);
    var barracks=catalog.Role(faction,"barracks");
    Check(s.Submit(Cmd(CommandKind.Build,worker.id,definition:barracks.id,position:new Point(18,8))).accepted,"barracks builds "+faction);
    for(int i=0;i<1000;i++)s.Step();
    Check(s.HasRole(0,"barracks"),"barracks completes "+faction);
    Check(s.Submit(Cmd(CommandKind.Build,worker.id,definition:catalog.Role(faction,"tech").id,position:new Point(8,8))).accepted,"tech prerequisites accepted "+faction);
    for(int i=0;i<1200;i++)s.Step();
    var tech=s.Entities.Values.First(e=>e.owner==0&&s.Type(e).role=="tech");
    Check(tech.complete&&s.Submit(Cmd(CommandKind.Research,tech.id,definition:"attack")).accepted,"research starts "+faction);
    Check(!s.Submit(Cmd(CommandKind.Research,tech.id,definition:"attack")).accepted,"duplicate queued research rejected "+faction);
    for(int i=0;i<800;i++)s.Step();
    Check(s.Players[0].attackLevel==1&&!s.Submit(Cmd(CommandKind.Research,tech.id,definition:"attack")).accepted,"research applies once "+faction);
    var extractor=catalog.Role(faction,"extractor");
    var gasResource=s.Resources.First(r=>r.definition=="camp0.catalyst");
    var extractorPosition=new Point(gasResource.position.x-extractor.footprint*.5f,gasResource.position.y-extractor.footprint*.5f);
    Check(!s.Submit(Cmd(CommandKind.Build,worker.id,definition:extractor.id,position:new Point(20,10))).accepted,"extractor requires deposit "+faction);
    Check(s.Submit(Cmd(CommandKind.Build,worker.id,definition:extractor.id,position:extractorPosition)).accepted,"extractor covers deposit "+faction);
    for(int i=0;i<1000;i++)s.Step();
    int gas=s.Players[0].catalyst;
    Check(s.Submit(Cmd(CommandKind.Gather,worker.id,target:gasResource.id)).accepted,"catalyst order accepted "+faction);
    for(int i=0;i<1200;i++)s.Step();
    Check(s.Players[0].catalyst>gas,"catalyst deposited "+faction);
}
s=New();hq=s.Entities.Values.First(e=>e.owner==0&&s.Type(e).role=="hq");worker=s.Entities.Values.First(e=>e.owner==0&&s.Type(e).role=="worker");
Check(!s.Submit(Cmd(CommandKind.Move,worker.id,position:new Point(float.NaN,0))).accepted,"nonfinite destination rejected");
foreach(var w in s.Entities.Values.Where(e=>e.owner==0&&!s.Type(e).building))w.position=new Point(25+w.id,25);
for(int y=11;y<=16;y++)for(int x=11;x<=16;x++)if(x==11||x==16||y==11||y==16)s.Map.Occupant[s.Map.Index(x,y)]=999;
Check(s.Submit(Cmd(CommandKind.Train,hq.id,definition:"industrial.worker")).accepted,"blocked exit still accepts production");
for(int i=0;i<300;i++)s.Step();
Check(s.Units(0)==6&&hq.queue.Count==1&&hq.queue[0].remaining==0,"finished unit waits at blocked exit");
s.Map.Occupant[s.Map.Index(11,11)]=0;s.Step();Check(s.Units(0)==7&&hq.queue.Count==0,"unit exits after path opens");
s=New();var mobile=s.Entities.Values.First(e=>e.owner==0&&!s.Type(e).building);var targetHq=s.Entities.Values.First(e=>e.owner==1&&s.Type(e).role=="hq");
mobile.position=new Point(110,114);mobile.order=CommandKind.Hold;for(int i=0;i<4;i++)s.Step();
Check(s.View(0).entities.Any(e=>e.id==targetHq.id&&!e.remembered),"scout reveals enemy building");
mobile.position=new Point(20,20);for(int i=0;i<4;i++)s.Step();
float observedHealth=s.View(0).entities.First(e=>e.id==targetHq.id).health;targetHq.health-=300;
Check(s.View(0).entities.First(e=>e.id==targetHq.id).remembered&&s.View(0).entities.First(e=>e.id==targetHq.id).health==observedHealth,"remembered buildings do not leak health changes");
Check(s.View(0).entities.Where(e=>e.owner==1).All(e=>e.queue.Length==0&&e.target==0),"enemy orders and queues never leak");
targetHq.lastDamageTick=s.Tick;
Check(s.View(0).entities.First(e=>e.id==targetHq.id).damageTick<0,"remembered buildings never expose live damage events");

// Damage feedback consumes actual hit timestamps, including splash, independently of health recovery.
foreach(string faction in new[]{"industrial","biological"})
{
    s=New(faction,faction);
    var artillery=catalog.Role(faction,"siege");var victimType=catalog.Role(faction,"worker");
    var shooter=new EntityState{id=1000,owner=0,definition=artillery.id,position=new Point(25,20),health=artillery.health,order=CommandKind.Hold};
    var victim=new EntityState{id=1001,owner=1,definition=victimType.id,position=new Point(28,20),health=victimType.health,order=CommandKind.Hold};
    var splash=new EntityState{id=1002,owner=1,definition=victimType.id,position=new Point(28,21),health=victimType.health,order=CommandKind.Hold};
    s.Entities.Add(shooter.id,shooter);s.Entities.Add(victim.id,victim);s.Entities.Add(splash.id,splash);
    for(int i=0;i<8&&victim.lastDamageTick<0;i++)s.Step();
    Check(victim.lastDamageTick>=0&&victim.health<victimType.health&&s.View(0).entities.First(e=>e.id==victim.id).damageTick==victim.lastDamageTick,"visible target exposes real received hit "+faction);
    Check(s.View(1).entities.First(e=>e.id==splash.id).damageTick==victim.lastDamageTick&&splash.health<victimType.health,"splash victims expose their own received hit "+faction);
    Check(s.View(0).entities.First(e=>e.id==shooter.id).damageTick<0,"firing does not trigger received damage feedback "+faction);
    var shot=s.View(0).attacks.Single(a=>a.attacker==shooter.id);
    Check(shooter.target==0&&shot.origin.Distance(s.Center(shooter))<.01f&&shot.destination.Distance(s.Center(victim))<.01f&&shot.tick==victim.lastDamageTick,"automatic fire records actual endpoints "+faction);
    Check(s.View(1).attacks.Any(a=>a.id==shot.id&&a.target==victim.id)&&shot.definition==artillery.id&&shot.targetDefinition==victimType.id,"visible defenders receive same weapon event and exact victim "+faction);
    var copy=s.View(0);copy.attacks[0].origin=new Point(0,0);
    Check(s.View(0).attacks.Single(a=>a.id==shot.id).origin.Distance(shot.origin)<.01f,"view cannot mutate authoritative weapon events "+faction);
    s.Paused=true;s.Step();s.Paused=false;
    Check(s.View(0).attacks.Count(a=>a.attacker==shooter.id)==1,"pause does not duplicate firing events "+faction);
    int hitTick=victim.lastDamageTick;victim.health=victimType.health;
    Check(s.View(1).entities.First(e=>e.id==victim.id).damageTick==hitTick,"health recovery does not create or erase hit timestamp "+faction);
    var targetType=catalog.Role(faction,"supply");
    var targetBuilding=new EntityState{id=1003,owner=1,definition=targetType.id,position=new Point(28,20),health=targetType.health};
    s.Entities.Remove(victim.id);s.Entities.Remove(splash.id);s.Entities.Add(targetBuilding.id,targetBuilding);
    shooter.order=CommandKind.Attack;shooter.target=targetBuilding.id;shooter.attackReadyTick=0;
    for(int i=0;i<8&&targetBuilding.lastDamageTick<0;i++)s.Step();
    Check(targetBuilding.health<targetType.health&&s.View(0).entities.First(e=>e.id==targetBuilding.id).damageTick==targetBuilding.lastDamageTick,"building receives visible hit feedback "+faction);
    Check(s.View(0).attacks.Last(a=>a.attacker==shooter.id).destination.Distance(s.Center(targetBuilding))<.01f,"building shot points to center rather than origin "+faction);
    targetBuilding.health=1;shooter.attackReadyTick=0;s.Step();
    var lethal=s.View(0).attacks.Last(a=>a.attacker==shooter.id);
    Check(!s.Entities.ContainsKey(targetBuilding.id)&&lethal.tick==s.Tick&&lethal.targetDefinition==targetBuilding.definition,"lethal shot remains renderable after target removal "+faction);
    shooter.order=CommandKind.Hold;
    for(int i=0;i<Simulation.TickRate;i++)s.Step();
    Check(s.View(0).attacks.All(a=>a.attacker!=shooter.id),"weapon history expires after one second "+faction);
}
// A long-range attacker can see a worker without being visible to its victim.
s=New();
var hiddenGun=new EntityState{id=2000,owner=0,definition="industrial.siege",position=new Point(26.95f,20.95f),health=250,order=CommandKind.Hold};
var distantWorker=new EntityState{id=2001,owner=1,definition="biological.worker",position=new Point(36.9f,20.95f),health=1000,order=CommandKind.Hold};
s.Entities.Add(hiddenGun.id,hiddenGun);s.Entities.Add(distantWorker.id,distantWorker);
for(int i=0;i<8&&!s.View(0).attacks.Any(a=>a.attacker==hiddenGun.id);i++)s.Step();
var hiddenShot=s.View(0).attacks.Single(a=>a.attacker==hiddenGun.id);
Check(!s.Visible(1,hiddenGun.position)&&s.View(1).attacks.All(a=>a.id!=hiddenShot.id),"weapon events do not reveal hidden attackers");
hiddenGun.attackReadyTick=10000;distantWorker.position=new Point(29,20);
for(int i=0;i<4;i++)s.Step();
Check(s.Visible(1,hiddenGun.position)&&s.View(1).attacks.All(a=>a.id!=hiddenShot.id),"new vision does not replay previously hidden fire");
hiddenGun.position=new Point(90,90);
for(int i=0;i<4;i++)s.Step();
Check(!s.Visible(0,hiddenShot.destination)&&s.View(0).attacks.All(a=>a.id!=hiddenShot.id),"lost vision removes visible weapon trails");
s=New();worker=s.Entities.Values.First(e=>e.owner==0&&s.Type(e).role=="worker");
Check(s.Submit(Cmd(CommandKind.Build,worker.id,definition:"industrial.supply",position:new Point(18,14))).accepted,"start interruptible industrial construction");
var unfinished=s.Entities.Values.First(e=>e.definition=="industrial.supply");
s.Submit(Cmd(CommandKind.Stop,worker.id));for(int i=0;i<500;i++)s.Step();
Check(!unfinished.complete,"industrial construction waits for worker");
Check(s.Submit(Cmd(CommandKind.Repair,worker.id,target:unfinished.id)).accepted,"context repair resumes unfinished building");
for(int i=0;i<1000;i++)s.Step();
Check(unfinished.complete,"resumed construction completes");

NavigationChecks.Run(catalog,map,Check);
PathfindingChecks.Run(catalog,Check);
CollisionChecks.Run(catalog,Check);
CombatMotionChecks.Run(catalog,Check);
CombatBehaviorChecks.Run(catalog,Check);
CasterChecks.Run(catalog,map,Check);
ProductionChecks.Run(catalog,map,Check);
MatchRecordingChecks.Run(catalog,map,Check);
MatchReportChecks.Run(catalog,map,Check);
GroupProductionChecks.Run(catalog,map,Check);
CommandQueueChecks.Run(catalog,Check);
ConstructionClearanceChecks.Run(catalog,Check);
RallyPointChecks.Run(catalog,Check);
HarvestingChecks.Run(catalog,map,Check);
HarvestingChecks.Depletion(catalog,new[]{map,training},Check);
WorkerActivityChecks.Run(catalog,map,Check);
DeathEventChecks.Run(catalog,map,Check);
DifficultyChecks.Run(catalog,map,training,Check);
AiEconomyChecks.Run(catalog,map,training,Check);
AiTacticsChecks.Run(catalog,Check);
AiTacticsChecks.Scouting(catalog,map,training,Check);
if(args.Contains("--practice"))DifficultyChecks.Practice(catalog,new[]{map,training},Check,root);

if(args.Contains("--matches"))
{
    int count = int.Parse(args.FirstOrDefault(a=>a.StartsWith("--count="))?.Substring(8) ?? "4");
    var outcomes = new List<object>();
    int startMatch = int.Parse(args.FirstOrDefault(a=>a.StartsWith("--start="))?.Substring(8) ?? "0");
    for(int match=startMatch;match<startMatch+count;match++)
    {
        string a=(match%4<2)?"industrial":"biological", b=(match%2==0)?"biological":"industrial";
        bool easy=match%8>=4;
        var opponent=args.FirstOrDefault(a=>a.StartsWith("--opponent="))?.Substring(11);
        var difficulty=opponent=="hard"?AiDifficulty.Hard:opponent=="easy"?AiDifficulty.Easy:opponent=="normal"?AiDifficulty.Normal:easy?AiDifficulty.Easy:AiDifficulty.Normal;
        var sim=New(a,b); var bots=new[]{new Strategist(0,AiDifficulty.Normal,catalog,map),new Strategist(1,difficulty,catalog,map)};
        var watch=Stopwatch.StartNew(); int rejected=0, accepted=0;
        for(int tick=0;tick<36000 && sim.Winner==-1;tick++)
        {
            if(tick%20==0)for(int player=0;player<2;player++)foreach(var command in bots[player].Decide(sim.View(player)))
            { if(sim.Submit(command).accepted)accepted++;else rejected++; }
            sim.Step();
            if(sim.Players.Any(p=>p.minerals<0||p.catalyst<0)||sim.Units(0)>Simulation.UnitLimit||sim.Units(1)>Simulation.UnitLimit)throw new Exception("Economy invariant violated");
            if(args.Contains("--trace") && tick%1200==0)Console.WriteLine($"t={tick/20}s units={sim.Units(0)}/{sim.Units(1)} buildings={sim.Buildings(0)}/{sim.Buildings(1)} resources={sim.Players[0].minerals}:{sim.Players[0].catalyst}/{sim.Players[1].minerals}:{sim.Players[1].catalyst}");
        }
        watch.Stop();
        var result=new{match,a,b,difficulty=difficulty.ToString(),winner=sim.Winner,seconds=sim.Tick/20,wallSeconds=watch.Elapsed.TotalSeconds,accepted,rejected,units0=sim.Units(0),units1=sim.Units(1),buildings0=sim.Buildings(0),buildings1=sim.Buildings(1)};
        outcomes.Add(result);Console.WriteLine(JsonSerializer.Serialize(result));
        if(args.Contains("--trace"))Console.WriteLine(JsonSerializer.Serialize(sim.Snapshot(),json));
        Check(sim.Winner != -1, "AI match " + match + " completes within 30 simulated minutes");
    }
    Directory.CreateDirectory(Path.Combine(root,"TestResults"));
    File.WriteAllText(Path.Combine(root,"TestResults/matches.json"),JsonSerializer.Serialize(outcomes,new JsonSerializerOptions{WriteIndented=true}));
}
Console.WriteLine($"{checks} checks passed.");
