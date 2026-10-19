using Frontier.Core;

static class AiTacticsChecks
{
    private static readonly string[] Factions={"industrial","biological"};
    private static readonly (AiDifficulty level,int opening,int scout,int reaction,float share,int max)[] Modes={
        (AiDifficulty.Easy,300,90,3,.65f,22),(AiDifficulty.Normal,210,60,2,.8f,32),(AiDifficulty.Hard,150,35,1,.95f,38)};

    public static void Run(Catalog catalog,Action<bool,string> check)
    {
        foreach(string faction in Factions)foreach(var mode in Modes)
        {
            string label=mode.level+"/"+faction;
            {
                var sim=Fixture(catalog,faction);Advance(sim,mode.opening*Simulation.TickRate);
                var army=Army(sim,20,new Point(16,16));var observer=Add(sim,0,"scout",new Point(45,27));army.Add(observer);
                var first=Add(sim,1,"supply",new Point(40,28));first.health=1;
                var second=Add(sim,1,"tech",new Point(52,31));Warm(sim,mode.reaction);
                var bot=new Strategist(0,mode.level,catalog,sim.Map.Definition);
                var orders=Execute(sim,bot,check,label+" opening");
                var attack=orders.FirstOrDefault(c=>c.kind==CommandKind.AttackMove);
                int expected=Math.Min(mode.max,Math.Max(mode.level==AiDifficulty.Easy?6:mode.level==AiDifficulty.Normal?9:10,(int)Math.Ceiling(army.Count*mode.share)));
                check(attack!=null&&attack.entities.Length==expected,"AI commits a proportional army instead of a tiny fixed wave: "+label);
                var attackerIds=new HashSet<int>(attack.entities);var start=army.Where(e=>attackerIds.Contains(e.id)).Average(e=>e.position.x);
                int repeated=0;bool retargeted=false;
                var lone=Add(sim,0,"ranged",new Point(16,24));bool loneSent=false;
                for(int tick=0;tick<mode.reaction*Simulation.TickRate;tick++)
                {
                    sim.Step();var extra=Execute(sim,bot,check,label+" waiting reinforcement");
                    loneSent|=extra.Any(c=>c.kind==CommandKind.AttackMove&&c.entities.Contains(lone.id));
                    retargeted|=extra.Any(c=>c.kind==CommandKind.AttackMove&&c.position.Distance(Center(sim,second))<.1f);
                }
                check(!loneSent,"AI holds a lone reinforcement until a useful group is ready: "+label);
                for(int i=0;i<4;i++)Add(sim,0,i%2==0?"melee":"ranged",new Point(18+i*1.5f,24));
                bool reinforced=false;
                for(int tick=0;tick<mode.reaction*Simulation.TickRate;tick++)
                {
                    sim.Step();var extra=Execute(sim,bot,check,label+" reinforcement batch");
                    reinforced|=extra.Any(c=>c.kind==CommandKind.AttackMove&&c.entities.Length>=Math.Max(3,(mode.level==AiDifficulty.Easy?6:mode.level==AiDifficulty.Normal?9:10)/2)&&c.entities.All(id=>!attackerIds.Contains(id)));
                    retargeted|=extra.Any(c=>c.kind==CommandKind.AttackMove&&c.position.Distance(Center(sim,second))<.1f);
                }
                check(reinforced,"AI reinforces the ongoing attack with a coordinated group: "+label);
                for(int tick=0;tick<Simulation.TickRate*24&&sim.Winner<0;tick++)
                {
                    sim.Step();var commands=Execute(sim,bot,check,label+" pressure");
                    repeated+=commands.Count(c=>c.kind==CommandKind.AttackMove&&c.position.Distance(new Point(41.5f,29.5f))<.1f);
                    retargeted|=commands.Any(c=>c.kind==CommandKind.AttackMove&&c.position.Distance(Center(sim,second))<.1f);
                }
                check(!sim.Entities.ContainsKey(first.id)&&retargeted,"AI destroys a target and redirects the same attack to the next known structure: "+label);
                check(army.Where(e=>attackerIds.Contains(e.id)&&sim.Entities.ContainsKey(e.id)).Average(e=>e.position.x)>start+8,
                    "AI orders actually carry the rear army forward: "+label);
                check(repeated<=2,"AI does not restart the same attack path every reaction: "+label);
            }
            {
                var sim=Fixture(catalog,faction);Advance(sim,mode.scout*Simulation.TickRate);
                var scout=Add(sim,0,"scout",new Point(15,13));Warm(sim,mode.reaction);
                var bot=new Strategist(0,mode.level,catalog,sim.Map.Definition);var before=scout.position;
                var first=Execute(sim,bot,check,label+" scouting");
                check(first.Any(c=>c.kind==CommandKind.Move&&c.entities.Contains(scout.id))&&!first.Any(c=>c.kind==CommandKind.AttackMove),
                    "AI sends an early scout without prematurely launching the main army: "+label);
                float traveled=0;bool revealed=false;
                for(int tick=0;tick<Simulation.TickRate*25;tick++)
                {
                    sim.Step();Execute(sim,bot,check,label+" scouting travel");traveled=Math.Max(traveled,scout.position.Distance(before));
                    revealed|=sim.View(0).entities.Any(e=>e.owner==1&&sim.Catalog[e.definition].building);
                }
                check(traveled>20&&revealed,"Scouting actually travels and reveals the enemy base through normal vision: "+label+" (distance="+traveled+", revealed="+revealed+")");
            }
            {
                var sim=Fixture(catalog,faction);Advance(sim,mode.reaction*Simulation.TickRate);
                var expansion=Add(sim,0,"hq",new Point(35,14));var troops=Army(sim,16,new Point(28,19));
                foreach(var troop in troops)troop.attackReadyTick=int.MaxValue;
                var intruder=Add(sim,1,"melee",new Point(39.5f,18.5f));intruder.attackReadyTick=int.MaxValue;
                Warm(sim,mode.reaction);foreach(var troop in troops)troop.attackReadyTick=0;
                var bot=new Strategist(0,mode.level,catalog,sim.Map.Definition);
                var commands=Execute(sim,bot,check,label+" expansion defense");
                var response=commands.FirstOrDefault(c=>c.kind==CommandKind.AttackMove&&c.position.Distance(intruder.position)<.1f);
                check(response!=null&&response.entities.Length>=2&&response.entities.Length<troops.Count,
                    "AI assigns a proportional force to defend an expansion: "+label);
                float health=intruder.health;
                for(int tick=0;tick<Simulation.TickRate*12&&sim.Entities.ContainsKey(intruder.id);tick++){sim.Step();Execute(sim,bot,check,label+" fighting intruder");}
                check(!sim.Entities.ContainsKey(intruder.id)||intruder.health<health,"Expansion defenders actually reach and damage the intruder: "+label);
            }
            {
                var sim=Fixture(catalog,faction);Advance(sim,mode.reaction*Simulation.TickRate);
                var defender=Add(sim,0,"ranged",new Point(22.5f,12.5f));defender.order=CommandKind.Stop;
                var enemy=Add(sim,1,"melee",new Point(27.5f,12.5f));enemy.attackReadyTick=int.MaxValue;
                Warm(sim,mode.reaction);var bot=new Strategist(0,mode.level,catalog,sim.Map.Definition);
                float health=enemy.health;var position=defender.position;
                var orders=Execute(sim,bot,check,label+" engaged reserve");
                check(!orders.Any(c=>(c.kind==CommandKind.Move||c.kind==CommandKind.Stop)&&c.entities.Contains(defender.id)),
                    "Staging does not cancel a reserve's real combat outside the base alarm radius: "+label);
                for(int tick=0;tick<Simulation.TickRate*8&&sim.Entities.ContainsKey(enemy.id);tick++)
                {sim.Step();Execute(sim,bot,check,label+" reserve combat continuity");}
                check((!sim.Entities.ContainsKey(enemy.id)||enemy.health<health-24)&&defender.position.Distance(position)<4,
                    "Engaged reserves continue dealing damage instead of shuttling back to assembly: "+label);
            }
            {
                var sim=Fixture(catalog,faction);Advance(sim,mode.reaction*Simulation.TickRate);
                var injured=Add(sim,0,"ranged",new Point(34.5f,21.5f));injured.health=sim.Type(injured).health*.15f;
                var enemy=Add(sim,1,"melee",new Point(38.5f,21.5f));enemy.attackReadyTick=int.MaxValue;
                Warm(sim,mode.reaction);var bot=new Strategist(0,mode.level,catalog,sim.Map.Definition);
                var orders=Execute(sim,bot,check,label+" retreat");
                check(orders.Any(c=>c.kind==CommandKind.Move&&c.entities.Contains(injured.id))
                    &&!orders.Any(c=>c.kind==CommandKind.Hold&&c.entities.Contains(injured.id))&&injured.order==CommandKind.Move,
                    "Retreat Move is not immediately cancelled by Hold: "+label);
                var start=injured.position;bool reached=false;
                for(int tick=0;tick<Simulation.TickRate*20;tick++)
                {
                    sim.Step();Execute(sim,bot,check,label+" retreat travel");
                    if(injured.order==CommandKind.Hold&&injured.position.Distance(new Point(6,6))<17){reached=true;break;}
                }
                check(reached&&injured.position.Distance(start)>15,"Retreat reaches the base before holding to regroup: "+label);
            }
            {
                var sim=Fixture(catalog,faction);Advance(sim,mode.reaction*Simulation.TickRate);
                var home=sim.Entities.Values.Single(e=>e.owner==0&&sim.Type(e).role=="hq");
                var safe=Add(sim,0,"hq",new Point(50,4));
                var injured=Add(sim,0,"ranged",new Point(25.5f,10.5f));injured.health=sim.Type(injured).health*.08f;
                var enemy=Add(sim,1,"melee",new Point(29.5f,10.5f));enemy.attackReadyTick=int.MaxValue;
                Warm(sim,mode.reaction);var bot=new Strategist(0,mode.level,catalog,sim.Map.Definition);
                var initial=Execute(sim,bot,check,label+" retreat before base loss");
                check(initial.Any(c=>c.kind==CommandKind.Move&&c.entities.Contains(injured.id)&&c.position.Distance(Center(sim,home))<15),
                    "Retreat initially selects the nearer headquarters: "+label);
                home.health=0;bool redirected=false,reached=false;
                for(int tick=0;tick<Simulation.TickRate*30;tick++)
                {
                    sim.Step();var commands=Execute(sim,bot,check,label+" retreat after base loss");
                    redirected|=commands.Any(c=>c.kind==CommandKind.Move&&c.entities.Contains(injured.id)&&c.position.Distance(Center(sim,safe))<15);
                    if(injured.order==CommandKind.Hold&&injured.position.Distance(Center(sim,safe))<17){reached=true;break;}
                }
                check(redirected&&reached,"Retreat redirects to a surviving base instead of holding at destroyed headquarters: "+label);
            }
        }
        foreach(string faction in Factions)
        {
            var sim=Fixture(catalog,faction);Advance(sim,Simulation.TickRate*2);
            var a=Add(sim,0,"caster",new Point(25.5f,20.5f));var b=Add(sim,0,"caster",new Point(25.5f,24.5f));
            var enemy=Add(sim,1,"melee",new Point(30.5f,20.5f));enemy.health=80;enemy.attackReadyTick=int.MaxValue;
            var hurt=Add(sim,0,"melee",new Point(27.5f,25.5f));hurt.health=sim.Type(hurt).health-70;hurt.attackReadyTick=int.MaxValue;
            Warm(sim,1);var bot=new Strategist(0,AiDifficulty.Hard,catalog,sim.Map.Definition);
            var orders=Execute(sim,bot,check,"spell coordination "+faction);float enemyHealth=enemy.health,allyHealth=hurt.health;
            check(orders.Count(c=>c.kind==CommandKind.Cast)==2,"Hard uses multiple ready casters in one reaction: "+faction);
            sim.Step();
            check(enemy.health<enemyHealth&&hurt.health>allyHealth&&a.abilityCooldowns.Count+b.abilityCooldowns.Count==2,
                "Coordinated casts apply useful damage and healing with authoritative energy and cooldowns: "+faction);
            var own=sim.View(0);var enemyView=sim.View(1);
            check(enemyView.entities.Where(e=>e.owner==0).All(e=>e.energy==0&&e.abilityCooldowns.Length==0),"Stronger AI still receives no enemy energy or cooldown data: "+faction);
        }
    }
    public static void Scouting(Catalog catalog,MapDefinition map,MapDefinition training,Action<bool,string> check)
    {
        foreach(var arena in new[]{map,training})
        {
            var sim=new Simulation(arena,catalog,"industrial","industrial");
            var bot=new Strategist(0,AiDifficulty.Easy,catalog,arena);
            var spawn=arena.spawns.Single(s=>s.player==0);var home=new Point(spawn.x+2,spawn.y+2);
            int scoutId=0,ordered=-1,leftHome=-1,revealed=-1;
            for(int tick=0;tick<180*Simulation.TickRate;tick++)
            {
                foreach(var command in Execute(sim,bot,check,arena.id+" real opening scout"))
                    if(scoutId==0&&command.kind==CommandKind.Move&&command.position.Distance(home)>18)
                    {
                        scoutId=command.entities.FirstOrDefault(id=>sim.Entities.TryGetValue(id,out var unit)&&sim.Type(unit).role=="scout");
                        if(scoutId!=0)ordered=sim.Tick/Simulation.TickRate;
                    }
                sim.Step();
                if(leftHome<0&&scoutId!=0&&sim.Entities.TryGetValue(scoutId,out var scout)&&scout.position.Distance(home)>18)
                    leftHome=sim.Tick/Simulation.TickRate;
                if(revealed<0&&sim.View(0).entities.Any(e=>e.owner==1&&catalog[e.definition].role=="hq"))revealed=sim.Tick/Simulation.TickRate;
                if(leftHome>=0&&revealed>=0)break;
            }
            string label="Easy industrial/"+arena.id;
            check(ordered>=0&&ordered<=90,"Authored-map opening issues the early scout order: "+label+" at="+ordered);
            check(leftHome>=0&&leftHome<=135,"The original scout escapes staged troops and actually leaves home before the assault: "+label+" at="+leftHome);
            check(revealed>=0&&revealed<180,"The early scout actually reveals the enemy headquarters before the main army: "+label+" at="+revealed);
        }
    }
    private static GameCommand[] Execute(Simulation sim,Strategist bot,Action<bool,string> check,string label)
    {
        var commands=bot.Decide(sim.View(0)).ToArray();
        foreach(var command in commands)
        {
            var result=sim.Submit(command);
            if(!result.accepted)check(false,"AI issued a rejected command ("+command.kind+"): "+result.message+" / "+label);
        }
        return commands;
    }
    private static List<EntityState> Army(Simulation sim,int count,Point start)
    {
        var result=new List<EntityState>();string[] roles={"melee","ranged","siege","caster"};
        for(int i=0;i<count;i++)result.Add(Add(sim,0,roles[i%roles.Length],new Point(start.x+i%5*1.5f,start.y+i/5*1.5f)));
        return result;
    }
    private static EntityState Add(Simulation sim,int owner,string role,Point point)
    {
        var type=sim.Catalog.Role(sim.Players[owner].faction,role);int id=sim.Entities.Keys.Max()+1;
        var entity=new EntityState{id=id,owner=owner,definition=type.id,position=point,health=type.health,energy=type.energyMax,order=CommandKind.Hold};
        sim.Entities.Add(id,entity);if(type.building)sim.Map.Occupy(point,type.footprint,id);return entity;
    }
    private static Point Center(Simulation sim,EntityState e)=>sim.Type(e).building?new Point(e.position.x+sim.Type(e).footprint*.5f,e.position.y+sim.Type(e).footprint*.5f):e.position;
    private static void Advance(Simulation sim,int tick){while(sim.Tick<tick)sim.Step();}
    private static void Warm(Simulation sim,int reaction){for(int i=0;i<4;i++)sim.Step();while(sim.Tick%(reaction*Simulation.TickRate)!=0)sim.Step();}
    private static Simulation Fixture(Catalog catalog,string faction)
    {
        var map=new MapDefinition{id="ai-tactics",title="AI tactics",width=80,height=64,
            spawns=new[]{new SpawnDefinition{player=0,x=4,y=4},new SpawnDefinition{player=1,x=68,y=50}},
            resources=new[]{new ResourceDefinition{id="a",x=12,y=13},new ResourceDefinition{id="b",x=62,y=45},
                new ResourceDefinition{id="c",kind="catalyst",x=9,y=10},new ResourceDefinition{id="d",kind="catalyst",x=72,y=43}}};
        var sim=new Simulation(map,catalog,faction,faction);
        foreach(var e in sim.Entities.Values.Where(e=>!sim.Type(e).building).ToArray())sim.Entities.Remove(e.id);
        sim.Players[0].minerals=sim.Players[0].catalyst=0;return sim;
    }
}
