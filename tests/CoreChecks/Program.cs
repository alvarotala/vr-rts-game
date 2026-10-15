using System.Diagnostics;
using System.Text.Json;
using Frontier.Core;

var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = false };
string root = args.FirstOrDefault(a => a.StartsWith("--root="))?.Substring(7) ?? Directory.GetCurrentDirectory();
T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(root, path)), json);
var catalog = new Catalog(Read<CatalogDefinition>("Assets/Frontier/Resources/catalog.json"));
var map = Read<MapDefinition>("Assets/Frontier/Resources/Maps/rift.json");
var training = Read<MapDefinition>("Assets/Frontier/Resources/Maps/training.json");
int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
void Throws(Action action, string name) { try { action(); } catch (ArgumentException) { Check(true, name); return; } Check(false, name); }
Simulation New(string a = "industrial", string b = "biological") => new Simulation(map, catalog, a, b);
GameCommand Cmd(CommandKind kind, int id, int player = 0, string definition = null, Point position = default, int target = 0) => new GameCommand { kind = kind, entities = new[] { id }, player = player, definition = definition, position = position, target = target };

Check(new GridMap(map).Count == 16384 && new GridMap(training).Count == 2304, "both authored maps validate");
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

// Building placement, growth, upgrades, fog memory and blocked production exercise complete behavior.
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
    Check(s.Submit(Cmd(CommandKind.Build,worker.id,definition:catalog.Role(faction,"tech").id,position:new Point(8,14))).accepted,"tech prerequisites accepted "+faction);
    for(int i=0;i<1200;i++)s.Step();
    var tech=s.Entities.Values.First(e=>e.owner==0&&s.Type(e).role=="tech");
    Check(tech.complete&&s.Submit(Cmd(CommandKind.Research,tech.id,definition:"attack")).accepted,"research starts "+faction);
    Check(!s.Submit(Cmd(CommandKind.Research,tech.id,definition:"attack")).accepted,"duplicate queued research rejected "+faction);
    for(int i=0;i<800;i++)s.Step();
    Check(s.Players[0].attackLevel==1&&!s.Submit(Cmd(CommandKind.Research,tech.id,definition:"attack")).accepted,"research applies once "+faction);
    var extractor=catalog.Role(faction,"extractor");
    Check(!s.Submit(Cmd(CommandKind.Build,worker.id,definition:extractor.id,position:new Point(20,10))).accepted,"extractor requires deposit "+faction);
    Check(s.Submit(Cmd(CommandKind.Build,worker.id,definition:extractor.id,position:new Point(11,24))).accepted,"extractor covers deposit "+faction);
    for(int i=0;i<1000;i++)s.Step();
    int gas=s.Players[0].catalyst;var gasResource=s.Resources.First(r=>r.kind=="catalyst"&&r.position.x<30);
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
s=New();worker=s.Entities.Values.First(e=>e.owner==0&&s.Type(e).role=="worker");
Check(s.Submit(Cmd(CommandKind.Build,worker.id,definition:"industrial.supply",position:new Point(18,14))).accepted,"start interruptible industrial construction");
var unfinished=s.Entities.Values.First(e=>e.definition=="industrial.supply");
s.Submit(Cmd(CommandKind.Stop,worker.id));for(int i=0;i<500;i++)s.Step();
Check(!unfinished.complete,"industrial construction waits for worker");
Check(s.Submit(Cmd(CommandKind.Repair,worker.id,target:unfinished.id)).accepted,"context repair resumes unfinished building");
for(int i=0;i<1000;i++)s.Step();
Check(unfinished.complete,"resumed construction completes");

NavigationChecks.Run(catalog,map,Check);

if(args.Contains("--matches"))
{
    int count = int.Parse(args.FirstOrDefault(a=>a.StartsWith("--count="))?.Substring(8) ?? "4");
    var outcomes = new List<object>();
    int startMatch = int.Parse(args.FirstOrDefault(a=>a.StartsWith("--start="))?.Substring(8) ?? "0");
    for(int match=startMatch;match<startMatch+count;match++)
    {
        string a=(match%4<2)?"industrial":"biological", b=(match%2==0)?"biological":"industrial";
        bool easy=match%8>=4;
        var sim=New(a,b); var bots=new[]{new Strategist(0,false,catalog,map),new Strategist(1,easy,catalog,map)};
        var watch=Stopwatch.StartNew(); int rejected=0, accepted=0;
        for(int tick=0;tick<36000 && sim.Winner==-1;tick++)
        {
            if(tick%20==0)for(int player=0;player<2;player++)foreach(var command in bots[player].Decide(sim.View(player)))
            { if(sim.Submit(command).accepted)accepted++;else rejected++; }
            sim.Step();
            if(sim.Players.Any(p=>p.minerals<0||p.catalyst<0)||sim.Units(0)>50||sim.Units(1)>50)throw new Exception("Economy invariant violated");
            if(args.Contains("--trace") && tick%1200==0)Console.WriteLine($"t={tick/20}s units={sim.Units(0)}/{sim.Units(1)} buildings={sim.Buildings(0)}/{sim.Buildings(1)} resources={sim.Players[0].minerals}:{sim.Players[0].catalyst}/{sim.Players[1].minerals}:{sim.Players[1].catalyst}");
        }
        watch.Stop();
        var result=new{match,a,b,easy,winner=sim.Winner,seconds=sim.Tick/20,wallSeconds=watch.Elapsed.TotalSeconds,accepted,rejected,units0=sim.Units(0),units1=sim.Units(1),buildings0=sim.Buildings(0),buildings1=sim.Buildings(1)};
        outcomes.Add(result);Console.WriteLine(JsonSerializer.Serialize(result));
        if(args.Contains("--trace"))Console.WriteLine(JsonSerializer.Serialize(sim.Snapshot(),json));
        Check(sim.Winner != -1, "AI match " + match + " completes within 30 simulated minutes");
    }
    Directory.CreateDirectory(Path.Combine(root,"TestResults"));
    File.WriteAllText(Path.Combine(root,"TestResults/matches.json"),JsonSerializer.Serialize(outcomes,new JsonSerializerOptions{WriteIndented=true}));
}
Console.WriteLine($"{checks} checks passed.");
