using System.Text.Json;
using Frontier.Core;

static class DifficultyChecks
{
    private static readonly string[] Roles={"ranged","melee","scout","siege","caster"};
    private static readonly string[] Factions={"industrial","biological"};
    private static readonly (AiDifficulty level,int reaction,int opening,int firstSize,int maxSize,int workers,int army)[] Modes={
        (AiDifficulty.Easy,3,300,6,22,16,28),
        (AiDifficulty.Normal,2,210,9,32,20,36),
        (AiDifficulty.Hard,1,150,10,38,22,38)};
    private static int Tick(int seconds)=>seconds*Simulation.TickRate;

    public static void Run(Catalog catalog,MapDefinition map,MapDefinition training,Action<bool,string> check)
    {
        check(Enum.GetValues<AiDifficulty>().SequenceEqual(Modes.Select(m=>m.level)),"Only Easy, Normal and Hard are available");
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
            foreach(var role in new[]{"barracks","barracks","barracks","supply","extractor","tech","academy","defense"})Add(role,new Point(20,8));
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
            var beforeReaction=View(mode.reaction);beforeReaction.tick--;
            check(!bot.Decide(beforeReaction).Any(),"Reaction delay: "+label);
            check(bot.Decide(View(mode.reaction)).Any(),"Reacts on its cadence: "+label);
            var before=bot.Decide(View(mode.opening-mode.reaction)).ToArray();
            check(!before.Any(c=>c.kind==CommandKind.AttackMove),"Main army respects its shorter opening: "+label);
            var orders=bot.Decide(View(mode.opening)).ToArray();
            var wave=orders.Where(c=>c.kind==CommandKind.AttackMove&&c.position.Distance(new Point(14,14))>14).OrderByDescending(c=>c.entities.Length).FirstOrDefault();
            check(wave!=null&&wave.entities.Length>=mode.firstSize&&wave.entities.Length<=mode.maxSize,
                "Ready army sends a substantial bounded attack at its opening: "+label);
            check(wave!=null&&wave.entities.Length>(mode.level==AiDifficulty.Easy?4:mode.level==AiDifficulty.Normal?6:8),
                "Initial pressure exceeds the previous difficulty: "+label);
            var threat=View(mode.reaction);
            threat.entities=threat.entities.Append(new VisibleEntity{id=999,owner=1,definition=catalog.Role(faction,"melee").id,position=new Point(16,14),health=100,complete=true}).ToArray();
            check(Bot().Decide(threat).Any(c=>(c.kind==CommandKind.AttackMove||c.kind==CommandKind.Attack)&&c.entities.Any(id=>threat.entities.Any(e=>e.id==id&&catalog[e.definition].role!="worker"))),
                "Base receives army defense during opening: "+label);
            threat.entities.Last().remembered=true;
            check(!Bot().Decide(threat).Any(c=>c.kind==CommandKind.AttackMove||c.kind==CommandKind.Attack),"Remembered enemies do not trigger immediate defense: "+label);
            threat.winner=0;check(!Bot().Decide(threat).Any(),"Finished match produces no orders: "+label);

            foreach(string role in Roles.Append("worker"))
            {
                var view=View(120);view.supply-=view.entities.Count(e=>catalog[e.definition].role==role);
                view.entities=view.entities.Where(e=>catalog[e.definition].role!=role).ToArray();
                var command=Bot().Decide(view).FirstOrDefault(c=>c.kind==CommandKind.Train&&catalog[c.definition].role==role);
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

            int academyAt=mode.level==AiDifficulty.Easy?160:mode.level==AiDifficulty.Hard?80:110;
            academyAt=((academyAt+mode.reaction-1)/mode.reaction)*mode.reaction;
            var academyPlan=View(academyAt-mode.reaction);
            academyPlan.entities=academyPlan.entities.Where(e=>catalog[e.definition].role!="academy").ToArray();
            check(!Bot().Decide(academyPlan).Any(c=>c.kind==CommandKind.Build&&catalog[c.definition].role=="academy"),"Caster building waits for its difficulty timing: "+label);
            academyPlan.tick=Tick(academyAt);
            check(Bot().Decide(academyPlan).Any(c=>c.kind==CommandKind.Build&&catalog[c.definition].role=="academy"),"Every difficulty builds caster production: "+label);

            PlayerView SpellView(out VisibleEntity caster)
            {
                var view=View(mode.reaction);caster=view.entities.First(e=>catalog[e.definition].role=="caster");
                caster.energy=catalog[caster.definition].energyMax;
                return view;
            }
            PlayerView WithEnemies(out VisibleEntity caster)
            {
                var view=SpellView(out caster);var point=new Point(caster.position.x+2,caster.position.y);
                view.entities=view.entities.Concat(new[]{
                    new VisibleEntity{id=8000,owner=1,definition=catalog.Role(faction,"melee").id,position=point,health=100,complete=true},
                    new VisibleEntity{id=8001,owner=1,definition=catalog.Role(faction,"ranged").id,position=new Point(point.x,point.y+1),health=90,complete=true}}).ToArray();
                return view;
            }
            var spellView=WithEnemies(out var caster);
            var damage=catalog[caster.definition].abilities.Select(catalog.Ability).Single(a=>a.effect=="damage");
            var heal=catalog[caster.definition].abilities.Select(catalog.Ability).Single(a=>a.effect=="heal");
            var cast=Bot().Decide(spellView).Single(c=>c.kind==CommandKind.Cast);
            check(cast.definition==damage.id&&cast.entities.SequenceEqual(new[]{caster.id})&&!cast.append,
                "Visible local enemies receive one normal caster activation: "+label);
            caster.energy=damage.energyCost-1;
            check(!Bot().Decide(spellView).Any(c=>c.kind==CommandKind.Cast),"AI cannot cast without enough energy: "+label);
            caster.energy=catalog[caster.definition].energyMax;caster.abilityCooldowns=new[]{new AbilityCooldown{ability=damage.id,readyTick=spellView.tick+1}};
            check(!Bot().Decide(spellView).Any(c=>c.kind==CommandKind.Cast),"AI respects ability cooldowns: "+label);
            caster.abilityCooldowns=Array.Empty<AbilityCooldown>();
            foreach(var foe in spellView.entities.Where(e=>e.owner==1))foe.remembered=true;
            check(!Bot().Decide(spellView).Any(c=>c.kind==CommandKind.Cast),"AI does not cast on remembered enemies: "+label);
            spellView=WithEnemies(out caster);
            foreach(var foe in spellView.entities.Where(e=>e.owner==1))foe.position=new Point(100,100);
            check(!Bot().Decide(spellView).Any(c=>c.kind==CommandKind.Cast),"AI only casts within its current ability range: "+label);
            spellView=WithEnemies(out caster);
            foreach(var foe in spellView.entities.Where(e=>e.owner==1))spellView.visibility[new GridMap(map).Cell(foe.position)]=1;
            check(!Bot().Decide(spellView).Any(c=>c.kind==CommandKind.Cast),"AI cannot aim abilities through fog: "+label);
            spellView=SpellView(out caster);
            var injured=spellView.entities.First(e=>catalog[e.definition].role=="ranged");injured.position=new Point(caster.position.x+1,caster.position.y);injured.health-=heal.amount;
            cast=Bot().Decide(spellView).Single(c=>c.kind==CommandKind.Cast);
            check(cast.definition==heal.id&&cast.position.Distance(injured.position)<=heal.radius,"AI heals an injured nearby ally: "+label);
            injured.health=catalog[injured.definition].health-1;
            check(!Bot().Decide(spellView).Any(c=>c.kind==CommandKind.Cast),"AI saves healing energy when allies only have a scratch: "+label);
            spellView=WithEnemies(out caster);
            sim.Entities[caster.id].energy=catalog[caster.definition].energyMax;
            foreach(var foe in spellView.entities.Where(e=>e.owner==1))
                sim.Entities[foe.id]=new EntityState{id=foe.id,owner=foe.owner,definition=foe.definition,position=foe.position,health=foe.health,order=CommandKind.Hold};
            bool castAccepted=false;
            foreach(var command in Bot().Decide(spellView))
            {
                var result=sim.Submit(command);
                if(command.kind==CommandKind.Cast)castAccepted=result.accepted;
            }
            sim.Step();
            check(castAccepted&&sim.Entities[caster.id].energy<catalog[caster.definition].energyMax
                &&sim.View(0).abilityEvents.Any(e=>e.caster==caster.id),"Army orders preserve the actual AI cast through the next tick: "+label);
        }
    }

    public static void Practice(Catalog catalog,MapDefinition[] maps,Action<bool,string> check,string root)
    {
        var records=new List<object>();
        foreach(var map in maps.OrderBy(m=>m.width))foreach(string faction in Factions)foreach(var mode in Modes)
        {
            var sim=new Simulation(map,catalog,"industrial",faction);var bot=new Strategist(1,mode.level,catalog,map);
            var home=sim.Entities.Values.First(e=>e.owner==1&&sim.Type(e).role=="hq").position;
            int firstAttack=-1,firstDamage=-1,maxWorkers=0,maxSoldiers=0,maxPopulation=0,maxWave=0,rejected=0,accepted=0,castsOrdered=0;
            var trained=new HashSet<string>();var built=new HashSet<string>();
            var castsExecuted=new HashSet<int>();
            var rejections=new Dictionary<string,int>();var samples=new List<object>();
            for(int tick=0;tick<Tick(24*60)&&sim.Winner==-1;tick++)
            {
                if(tick%Simulation.TickRate==0)
                {
                    var view=sim.View(1);foreach(var effect in view.abilityEvents)if(effect.owner==1)castsExecuted.Add(effect.id);
                    foreach(var command in bot.Decide(view))
                    {
                        bool offensive=command.kind==CommandKind.AttackMove&&command.position.Distance(home)>14;
                        var result=sim.Submit(command);
                        if(!result.accepted)
                        { rejected++;rejections[result.message]=rejections.GetValueOrDefault(result.message)+1;continue; }
                        accepted++;
                        if(offensive){if(firstAttack<0)firstAttack=tick;maxWave=Math.Max(maxWave,command.entities.Length);}
                        if(command.kind==CommandKind.Train)trained.Add(catalog[command.definition].role);
                        if(command.kind==CommandKind.Build)built.Add(catalog[command.definition].role);
                        if(command.kind==CommandKind.Cast)castsOrdered++;
                    }
                }
                sim.Step();
                if(firstDamage<0&&sim.Entities.Values.Any(e=>e.owner==0&&e.lastDamageTick>=0))firstDamage=tick;
                if(tick%Simulation.TickRate!=0)continue;
                var own=sim.Entities.Values.Where(e=>e.owner==1).ToArray();
                maxPopulation=Math.Max(maxPopulation,sim.Units(1)+sim.Reserved(1));
                maxWorkers=Math.Max(maxWorkers,own.Count(e=>sim.Type(e).role=="worker")+own.Sum(e=>e.queue.Count(q=>!q.research&&catalog[q.definition].role=="worker")));
                maxSoldiers=Math.Max(maxSoldiers,own.Count(e=>!sim.Type(e).building&&sim.Type(e).role!="worker")+own.Sum(e=>e.queue.Count(q=>!q.research&&catalog[q.definition].role!="worker")));
                if(sim.Players.Any(p=>p.minerals<0||p.catalyst<0)||sim.Units(1)+sim.Reserved(1)>sim.Capacity(1))throw new Exception("Practice economy invariant violated");
                if(tick%Tick(120)==0)samples.Add(new{seconds=tick/20,workers=own.Count(e=>sim.Type(e).role=="worker"),army=own.Count(e=>!sim.Type(e).building&&sim.Type(e).role!="worker"),sim.Players[1].minerals,sim.Players[1].catalyst});
            }
            foreach(var effect in sim.View(1).abilityEvents)if(effect.owner==1)castsExecuted.Add(effect.id);
            var record=new{map=map.id,faction,difficulty=mode.level.ToString(),firstAttackSeconds=firstAttack/20,firstDamageSeconds=firstDamage/20,maxWorkers,maxSoldiers,maxPopulation,maxWave,trained,built,castsOrdered,castsExecuted=castsExecuted.Count,accepted,rejected,rejections,seconds=sim.Tick/20,winner=sim.Winner,samples};
            records.Add(record);Console.WriteLine(JsonSerializer.Serialize(record));
            Directory.CreateDirectory(Path.Combine(root,"TestResults"));
            File.WriteAllText(Path.Combine(root,"TestResults/difficulty-practice.json"),JsonSerializer.Serialize(records,new JsonSerializerOptions{WriteIndented=true}));
            string label=$"{map.id}/{faction}/{mode.level}";
            check(maxWorkers<=mode.workers&&maxPopulation<=Math.Min(Simulation.UnitLimit,mode.workers+mode.army),"Practice population includes reservations: "+label);
            check(new[]{"worker","melee","ranged"}.All(trained.Contains),"Practice produces workers and complementary frontline troops: "+label);
            check(built.Contains("tech")&&(built.Contains("academy")||sim.Winner==1&&sim.Tick<Tick(mode.opening+180)),"Practice advances technology or wins before later tech is needed: "+label);
            check(firstAttack>=Tick(mode.opening)&&firstAttack<=Tick(mode.opening+120),"Practice launches on time without early attacks: "+label);
            check(firstDamage>=0&&firstDamage<=Tick(mode.opening+300),"Practice converts army pressure into actual damage: "+label);
            check(maxWave<=mode.maxSize,"Practice attack groups respect their configured limit: "+label);
            check(sim.Winner==1,"Practice still defeats a completely idle opponent: "+label);
        }
    }
}
