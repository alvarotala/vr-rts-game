using System.Text.Json;
using Frontier.Core;

static class DifficultyChecks
{
    private static readonly string[] Roles={"ranged","melee","scout","siege"};
    private static readonly string[] Factions={"industrial","biological"};
    private static readonly (AiDifficulty level,int reaction,int opening,int firstSize,int maxSize,int rest,int workers,int army)[] Modes={
        (AiDifficulty.VeryEasy,6,720,3,5,180,11,12),
        (AiDifficulty.Easy,4,600,4,7,150,12,18),
        (AiDifficulty.Normal,2,480,6,16,90,16,28)};
    private static int Tick(int seconds)=>seconds*Simulation.TickRate;

    public static void Run(Catalog catalog,MapDefinition map,MapDefinition training,Action<bool,string> check)
    {
        foreach(var arena in new[]{map,training})foreach(var first in Factions)foreach(var second in Factions)foreach(var mode in Modes)
        {
            var sim=new Simulation(arena,catalog,first,second);
            _=new Strategist(1,mode.level,catalog,arena);
            bool equal=sim.Players.All(p=>p.minerals==400&&p.catalyst==0&&p.attackLevel==0&&p.armorLevel==0)
                && Enumerable.Range(0,2).All(p=>sim.Units(p)==6&&sim.Buildings(p)==1&&sim.Capacity(p)==10)
                && sim.Entities.Values.All(e=>e.complete&&e.health==sim.Type(e).health&&e.queue.Count==0
                    && (sim.Type(e).role=="worker"||sim.Type(e).role=="hq"));
            check(equal,$"Equal opening economy and full faction health: {arena.id}/{first}/{second}/{mode.level}");
            var firstWorkers=sim.Entities.Values.Where(e=>e.owner==0&&sim.Type(e).role=="worker").OrderBy(e=>e.id).ToArray();
            var secondWorkers=sim.Entities.Values.Where(e=>e.owner==1&&sim.Type(e).role=="worker").OrderBy(e=>e.id).ToArray();
            check(firstWorkers.Zip(secondWorkers).All(pair=>new Point(arena.width-pair.First.position.x,arena.height-pair.First.position.y)
                .Distance(pair.Second.position)<.01f),$"Mirrored initial worker positions: {arena.id}/{first}/{second}/{mode.level}");
            check(firstWorkers.Zip(secondWorkers).All(pair=>{
                var a=sim.Resources.Single(r=>r.id==pair.First.resourceId);var b=sim.Resources.Single(r=>r.id==pair.Second.resourceId);
                return new Point(arena.width-a.position.x,arena.height-a.position.y).Distance(b.position)<.01f;
            }),$"Mirrored initial gathering assignments: {arena.id}/{first}/{second}/{mode.level}");
            var camps=arena.spawns.OrderBy(s=>s.player).Select(s=>arena.resources
                .Where(r=>new Point(r.x+.5f,r.y+.5f).Distance(new Point(s.x+2,s.y+2))<18)
                .Select(r=>$"{r.kind}:{r.amount}").OrderBy(r=>r).ToArray()).ToArray();
            check(camps[0].SequenceEqual(camps[1]),$"Equal nearby starting deposits: {arena.id}/{mode.level}");
        }
        foreach(var faction in Factions)foreach(var mode in Modes)
        {
            var sim=new Simulation(map,catalog,faction);int id=1000;
            EntityState Add(string role,Point position)
            {
                var type=catalog.Role(faction,role);
                var entity=new EntityState{id=id++,owner=0,definition=type.id,health=type.health,position=position};
                sim.Entities.Add(entity.id,entity);return entity;
            }
            foreach(var role in new[]{"barracks","barracks","supply","extractor","tech","defense"})Add(role,new Point(20,8));
            for(int i=6;i<mode.workers;i++)Add("worker",new Point(16,16));
            for(int i=0;i<mode.army;i++)Add(Roles[i%Roles.Length],new Point(15+i%5,16+i/5));
            PlayerView View(int seconds)
            {
                var view=sim.View(0);view.tick=Tick(seconds);view.minerals=5000;view.catalyst=5000;view.capacity=Simulation.UnitLimit;
                return view;
            }
            Strategist Bot()=>new Strategist(0,mode.level,catalog,map);
            var bot=Bot();string label=$"{mode.level}/{faction}";
            var nearLimit=View(mode.reaction);nearLimit.capacity=Simulation.UnitLimit-10;nearLimit.supply=nearLimit.capacity-1;
            check(Bot().Decide(nearLimit).Any(c=>c.kind==CommandKind.Build&&c.definition==catalog.Role(faction,"supply").id),"Supply planning can grow beyond the old population cap: "+label);
            check(!bot.Decide(View(mode.reaction-1)).Any(),"Reaction delay: "+label);
            check(bot.Decide(View(mode.reaction)).Any(),"Reacts on its cadence: "+label);
            var before=bot.Decide(View(mode.opening-mode.reaction)).ToArray();
            check(!before.Any(c=>c.kind==CommandKind.AttackMove),"No offensive orders during learning period: "+label);
            check(before.Where(c=>c.kind==CommandKind.Move).All(c=>c.position.Distance(new Point(14,14))<=12),"Scouts and reserves remain home: "+label);
            var orders=bot.Decide(View(mode.opening)).ToArray();
            var wave=orders.Single(c=>c.kind==CommandKind.AttackMove);
            check(wave.entities.Length==mode.firstSize&&wave.entities.Length<mode.army,"First attack is a small detachment: "+label);
            check(!bot.Decide(View(mode.opening+mode.reaction)).Any(c=>c.kind==CommandKind.AttackMove),"No constant reinforcement or repeated attack orders: "+label);
            orders=bot.Decide(View(mode.opening+120)).ToArray();
            check(orders.Any(c=>c.kind==CommandKind.Move&&wave.entities.All(c.entities.Contains))
                && !orders.Any(c=>c.kind==CommandKind.AttackMove),"Surviving wave is recalled before a new one: "+label);
            check(!bot.Decide(View(mode.opening+120+mode.rest-mode.reaction)).Any(c=>c.kind==CommandKind.AttackMove),"Recovery period begins after wave ends: "+label);
            int next=(int)Math.Ceiling((mode.opening+120+mode.rest)/(double)mode.reaction)*mode.reaction;
            check(bot.Decide(View(next)).Single(c=>c.kind==CommandKind.AttackMove).entities.Length==mode.firstSize+(mode.level==AiDifficulty.Normal?2:1),"Next wave grows gradually: "+label);
            for(int cycle=0;cycle<10;cycle++)
            {
                bot.Decide(View(next+120)).ToArray();
                next=(int)Math.Ceiling((next+120+mode.rest)/(double)mode.reaction)*mode.reaction;
                var laterWave=bot.Decide(View(next)).Single(c=>c.kind==CommandKind.AttackMove);
                check(laterWave.entities.Length<=mode.maxSize,"Later waves stay bounded "+cycle+": "+label);
            }

            var casualtyBot=Bot();var firstWave=casualtyBot.Decide(View(mode.opening)).Single(c=>c.kind==CommandKind.AttackMove);
            PlayerView Defeated(int seconds){var view=View(seconds);view.entities=view.entities.Where(e=>!firstWave.entities.Contains(e.id)).ToArray();return view;}
            check(!casualtyBot.Decide(Defeated(mode.opening+mode.reaction)).Any(c=>c.kind==CommandKind.AttackMove),"Destroyed wave does not trigger immediate replacement: "+label);
            check(!casualtyBot.Decide(Defeated(mode.opening+mode.rest)).Any(c=>c.kind==CommandKind.AttackMove),"Loss leaves a complete recovery interval: "+label);
            var cleanupBot=Bot();var objectives=View(mode.opening);
            objectives.entities=objectives.entities.Concat(new[]{
                new VisibleEntity{id=900,owner=1,definition=catalog.Role(faction,"hq").id,position=new Point(100,100)},
                new VisibleEntity{id=901,owner=1,definition=catalog.Role(faction,"tech").id,position=new Point(108,108)}}).ToArray();
            var detachment=cleanupBot.Decide(objectives).Single(c=>c.kind==CommandKind.AttackMove);
            objectives.tick+=Tick(mode.reaction);objectives.entities=objectives.entities.Where(e=>e.id!=900).ToArray();
            var cleanup=cleanupBot.Decide(objectives).Single(c=>c.kind==CommandKind.AttackMove);
            check(cleanup.entities.OrderBy(i=>i).SequenceEqual(detachment.entities.OrderBy(i=>i))&&cleanup.position.Distance(new Point(108,108))<.01f,
                "Same wave continues to known buildings after destroying its target: "+label);

            var threat=View(mode.reaction);
            threat.entities=threat.entities.Append(new VisibleEntity{id=999,owner=1,definition=catalog.Role(faction,"melee").id,position=new Point(16,14),health=100}).ToArray();
            check(Bot().Decide(threat).Any(c=>c.kind==CommandKind.AttackMove&&c.position.Distance(new Point(16,14))<.01f),"Base defends during opening: "+label);
            threat.entities.Last().remembered=true;
            check(!Bot().Decide(threat).Any(c=>c.kind==CommandKind.AttackMove),"Remembered enemies do not trigger defense: "+label);
            threat.winner=0;check(!Bot().Decide(threat).Any(),"Finished match produces no orders: "+label);
            var pursuit=View(mode.reaction);
            var pursuer=pursuit.entities.First(e=>catalog[e.definition].role=="melee");
            pursuer.order=CommandKind.AttackMove;pursuer.position=new Point(40,40);
            check(Bot().Decide(pursuit).Any(c=>c.kind==CommandKind.Move&&c.entities.Contains(pursuer.id)
                &&c.position.Distance(new Point(14,14))<=12),"Defender returns when intruder leaves the base: "+label);

            foreach(string role in Roles.Append("worker"))
            {
                var view=View(120);view.entities=view.entities.Where(e=>catalog[e.definition].role!=role).ToArray();
                var command=Bot().Decide(view).SingleOrDefault(c=>c.kind==CommandKind.Train&&catalog[c.definition].role==role);
                check(command!=null,"Uses available unit role "+role+": "+label);
            }
            foreach(string upgrade in new[]{"attack","armor"})
            {
                var view=View(1200);view.researched=upgrade=="armor"?new[]{"attack"}:Array.Empty<string>();
                check(Bot().Decide(view).Any(c=>c.kind==CommandKind.Research&&c.definition==upgrade),"Can research "+upgrade+": "+label);
            }
            // Replace live units with paid reservations. Neither multiple producers nor
            // several headquarters may exceed the strategy's target in a single decision.
            var queued=View(120);
            foreach(string role in Roles.Append("worker"))
            {
                var unit=queued.entities.First(e=>catalog[e.definition].role==role);
                queued.entities=queued.entities.Where(e=>e.id!=unit.id).ToArray();
                var producer=queued.entities.First(e=>catalog[e.definition].role==catalog[unit.definition].producer);
                producer.queue=producer.queue.Append(new ProductionOrder{definition=unit.definition,remaining=100,total=200}).ToArray();
            }
            check(!Bot().Decide(queued).Any(c=>c.kind==CommandKind.Train),"Population targets include all production reservations: "+label);
            var poor=View(1200);poor.minerals=49;poor.catalyst=0;
            check(!Bot().Decide(poor).Any(c=>c.kind==CommandKind.Train||c.kind==CommandKind.Build||c.kind==CommandKind.Research),"No unaffordable production orders: "+label);
        }
    }

    public static void Practice(Catalog catalog,MapDefinition[] maps,Action<bool,string> check,string root)
    {
        var records=new List<object>();
        foreach(var map in maps.OrderBy(m=>m.width))foreach(string faction in Factions)foreach(var mode in Modes)
        {
            var sim=new Simulation(map,catalog,"industrial",faction);var bot=new Strategist(1,mode.level,catalog,map);
            var home=sim.Entities.Values.First(e=>e.owner==1&&sim.Type(e).role=="hq").position;
            int firstAttack=-1,firstDamage=-1,maxWorkers=0,maxSoldiers=0,maxWave=0,rejected=0,accepted=0;
            var trained=new HashSet<string>();var built=new HashSet<string>();
            var rejections=new Dictionary<string,int>();var samples=new List<object>();
            for(int tick=0;tick<Tick(24*60)&&sim.Winner==-1;tick++)
            {
                if(tick%Simulation.TickRate==0)foreach(var command in bot.Decide(sim.View(1)))
                {
                    bool offensive=command.kind==CommandKind.AttackMove&&command.position.Distance(home)>14;
                    var result=sim.Submit(command);
                    if(!result.accepted)
                    { rejected++;rejections[result.message]=rejections.GetValueOrDefault(result.message)+1;continue; }
                    accepted++;
                    if(offensive){if(firstAttack<0)firstAttack=tick;maxWave=Math.Max(maxWave,command.entities.Length);}
                    if(command.kind==CommandKind.Train)trained.Add(catalog[command.definition].role);
                    if(command.kind==CommandKind.Build)built.Add(catalog[command.definition].role);
                }
                sim.Step();
                if(firstDamage<0&&sim.Entities.Values.Any(e=>e.owner==0&&e.lastDamageTick>=0))firstDamage=tick;
                if(tick%Simulation.TickRate!=0)continue;
                var own=sim.Entities.Values.Where(e=>e.owner==1).ToArray();
                maxWorkers=Math.Max(maxWorkers,own.Count(e=>sim.Type(e).role=="worker")+own.Sum(e=>e.queue.Count(q=>!q.research&&catalog[q.definition].role=="worker")));
                maxSoldiers=Math.Max(maxSoldiers,own.Count(e=>!sim.Type(e).building&&sim.Type(e).role!="worker")+own.Sum(e=>e.queue.Count(q=>!q.research&&catalog[q.definition].role!="worker")));
                if(sim.Players.Any(p=>p.minerals<0||p.catalyst<0)||sim.Units(1)+sim.Reserved(1)>sim.Capacity(1))throw new Exception("Practice economy invariant violated");
                if(tick%Tick(120)==0)samples.Add(new{seconds=tick/20,workers=own.Count(e=>sim.Type(e).role=="worker"),army=own.Count(e=>!sim.Type(e).building&&sim.Type(e).role!="worker"),sim.Players[1].minerals,sim.Players[1].catalyst});
            }
            var record=new{map=map.id,faction,difficulty=mode.level.ToString(),firstAttackSeconds=firstAttack/20,firstDamageSeconds=firstDamage/20,maxWorkers,maxSoldiers,maxWave,trained,built,accepted,rejected,rejections,seconds=sim.Tick/20,winner=sim.Winner,samples};
            records.Add(record);Console.WriteLine(JsonSerializer.Serialize(record));
            Directory.CreateDirectory(Path.Combine(root,"TestResults"));
            File.WriteAllText(Path.Combine(root,"TestResults/difficulty-practice.json"),JsonSerializer.Serialize(records,new JsonSerializerOptions{WriteIndented=true}));
            string label=$"{map.id}/{faction}/{mode.level}";
            check(maxWorkers<=mode.workers&&maxSoldiers<=mode.army,"Practice population includes reservations: "+label);
            check(Roles.Append("worker").All(trained.Contains),"Practice actually trains every unit type: "+label);
            check(firstAttack>=Tick(mode.opening)&&firstAttack<=Tick(mode.opening+120),"Practice launches on time without early attacks: "+label);
            check(firstDamage>=Tick(mode.opening),"Passive learner is safe throughout opening: "+label);
            check(maxWave<=mode.maxSize,"Practice never sends whole army: "+label);
            check(sim.Winner==1,"Practice still defeats a completely idle opponent: "+label);
        }
    }
}
