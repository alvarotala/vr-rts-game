using Frontier.Core;
using System.Text.Json;

static class CasterChecks
{
    public static void Run(Catalog catalog, MapDefinition map, Action<bool,string> check)
    {
        foreach (string faction in new[] { "industrial", "biological" })
        {
            string prefix = faction + " caster: ";
            var type = catalog.Role(faction,"caster");
            var damage = catalog.Ability(type.abilities.First(id=>catalog.Ability(id).effect=="damage"));
            var heal = catalog.Ability(type.abilities.First(id=>catalog.Ability(id).effect=="heal"));
            var academy = catalog.Role(faction,"academy");
            check(academy.building && academy.requires=="tech" && type.producer=="academy" && type.abilities.Length==2,
                prefix+"dedicated advanced producer and two active abilities");
            Production(catalog,map,faction,check);

            {
                var sim=Fixture(catalog,faction); var caster=Add(sim,0,"caster",new Point(20.5f,20.5f));
                var enemy=Add(sim,1,"ranged",new Point(25.5f,20.5f));
                var structure=Add(sim,1,"supply",new Point(25,22));
                var ally=Add(sim,0,"melee",new Point(24.5f,18.5f));
                Warm(sim); float before=enemy.health,buildingHealth=structure.health,allyHealth=ally.health;
                check(Cast(sim,caster,damage.id,enemy.position).accepted && caster.order==CommandKind.Cast && caster.energy==type.energyMax,
                    prefix+"accepted cast waits for simulation before charging");
                sim.Step();
                check(Near(before-enemy.health,damage.amount) && Near(buildingHealth-structure.health,damage.amount) && Near(ally.health,allyHealth),
                    prefix+"area damage affects visible enemy troops and buildings, without friendly fire");
                check(Near(caster.energy,type.energyMax-damage.energyCost) && caster.abilityCooldowns.Single().readyTick==sim.Tick+damage.cooldown,
                    prefix+"spends energy and starts cooldown exactly once");
                check(caster.order==CommandKind.Hold && sim.View(0).abilityEvents.Length==1,
                    prefix+"returns to hold and projects one visible effect");
                var own=sim.View(0).entities.Single(e=>e.id==caster.id); var enemyView=sim.View(1).entities.Single(e=>e.id==caster.id);
                check(Near(own.energy,caster.energy) && own.abilityCooldowns.Length==1 && enemyView.energy==0 && enemyView.abilityCooldowns.Length==0,
                    prefix+"energy and cooldown projection belongs only to the owner");
                own.abilityCooldowns[0].readyTick=0;
                check(caster.abilityCooldowns[0].readyTick>sim.Tick,prefix+"view cooldown objects cannot mutate authority");
                float energy=caster.energy;
                check(!Cast(sim,caster,damage.id,enemy.position).accepted && caster.energy==energy,prefix+"cooldown rejection is free");
                Steps(sim,20);
                check(Near(caster.energy,energy+type.energyRegen) && sim.View(0).abilityEvents.Length==0,
                    prefix+"energy regenerates per second and visual effects expire");
                sim.Paused=true; energy=caster.energy; int tick=sim.Tick;
                check(!Cast(sim,caster,heal.id,ally.position).accepted,prefix+"paused command rejected"); Steps(sim,20);
                check(caster.energy==energy && sim.Tick==tick,prefix+"pause freezes energy and cooldown clock");
                sim.Paused=false; Steps(sim,damage.cooldown);
                check(Cast(sim,caster,damage.id,enemy.position).accepted,prefix+"ability becomes usable after cooldown");
                sim.Submit(Order(caster,CommandKind.Stop)); caster.energy=type.energyMax-.01f; sim.Step();
                check(caster.energy==type.energyMax,prefix+"energy regeneration caps at maximum");
            }

            {
                var sim=Fixture(catalog,faction);var caster=Add(sim,0,"caster",new Point(20.5f,20.5f));
                var ally=Add(sim,0,"melee",new Point(24.5f,20.5f));var full=Add(sim,0,"ranged",new Point(24.5f,18.5f));
                var building=Add(sim,0,"supply",new Point(25,22));var enemy=Add(sim,1,"melee",new Point(26.5f,20.5f));
                Warm(sim); ally.health=sim.Type(ally).health-10; building.health-=40; enemy.health-=40;
                foreach(var unit in sim.Entities.Values)unit.lastDamageTick=sim.Tick;
                float buildingHealth=building.health,enemyHealth=enemy.health;
                check(Cast(sim,caster,heal.id,ally.position).accepted,prefix+"healing can be targeted on friendly ground");sim.Step();
                check(ally.health==sim.Type(ally).health && full.health==sim.Type(full).health && building.health==buildingHealth && enemy.health==enemyHealth,
                    prefix+"healing caps friendly troops and excludes buildings and enemies");
                caster.energy=0;
                check(!Cast(sim,caster,damage.id,ally.position).accepted && caster.energy==0,prefix+"insufficient energy never changes orders or charges");
            }

            {
                var sim=Fixture(catalog,faction);var caster=Add(sim,0,"caster",new Point(20.5f,20.5f));Warm(sim);
                string other=catalog.Role(faction=="industrial"?"biological":"industrial","caster").abilities[0];
                foreach(var command in new[]{
                    new GameCommand{player=1,kind=CommandKind.Cast,entities=new[]{caster.id},definition=damage.id,position=caster.position},
                    new GameCommand{player=0,kind=CommandKind.Cast,entities=new[]{caster.id},definition=other,position=caster.position},
                    new GameCommand{player=0,kind=CommandKind.Cast,entities=new[]{caster.id},definition="missing",position=caster.position},
                    new GameCommand{player=0,kind=CommandKind.Cast,entities=new[]{caster.id},definition=damage.id,position=new Point(float.NaN,1)},
                    new GameCommand{player=0,kind=CommandKind.Cast,entities=new[]{caster.id},definition=damage.id,position=new Point(float.PositiveInfinity,1)},
                    new GameCommand{player=0,kind=CommandKind.Cast,entities=new[]{caster.id},definition=damage.id,position=new Point(-1,1)},
                    new GameCommand{player=0,kind=CommandKind.Cast,entities=new[]{caster.id},definition=damage.id,position=new Point(60,30)},
                    new GameCommand{player=0,kind=CommandKind.Cast,entities=new[]{caster.id},definition=damage.id,position=caster.position,append=true}})
                    check(!sim.Submit(command).accepted && caster.energy==type.energyMax && caster.order==CommandKind.Hold,
                        prefix+"invalid ownership, ability, coordinate, fog or queue is harmless: "+command.definition+"/"+command.position.x+"/"+command.player+"/"+command.append);
                caster.complete=false;
                check(!Cast(sim,caster,damage.id,caster.position).accepted,prefix+"unfinished caster cannot cast");caster.complete=true;caster.health=0;
                check(!Cast(sim,caster,damage.id,caster.position).accepted,prefix+"dead caster cannot cast");
            }

            {
                var sim=Fixture(catalog,faction);var far=Add(sim,0,"caster",new Point(16.5f,20.5f));
                var near=Add(sim,0,"caster",new Point(23.5f,20.5f));var troop=Add(sim,0,"ranged",new Point(21.5f,18.5f));
                Warm(sim);var selected=new[]{troop.id,far.id,near.id};var point=new Point(29.5f,20.5f);
                check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Cast,entities=selected,definition=damage.id,position=point}).accepted
                    && near.order==CommandKind.Cast && far.order==CommandKind.Hold && troop.order==CommandKind.Hold,
                    prefix+"mixed selection picks one in-range caster and leaves army intent intact");
                check(sim.Submit(new GameCommand{player=0,kind=CommandKind.Cast,entities=selected,definition=damage.id,position=point}).accepted
                    && far.order==CommandKind.Cast,prefix+"repeated deliberate cast uses another ready caster before interrupting busy caster");
                sim.Step();check(near.abilityCooldowns.Count==1 && far.abilityCooldowns.Count==0,prefix+"distant caster pays only when its approach finishes");
            }

            foreach(var original in new[]{CommandKind.Move,CommandKind.AttackMove,CommandKind.Hold,CommandKind.Stop,CommandKind.Attack})
            {
                var sim=Fixture(catalog,faction);var caster=Add(sim,0,"caster",new Point(20.5f,20.5f));
                var scout=Add(sim,0,"scout",new Point(31.5f,17.5f));var enemy=Add(sim,1,"supply",new Point(33,21));
                Warm(sim);var resume=new Point(16.5f,24.5f);
                var cmd=Order(caster,original,resume);cmd.sequence=1;cmd.target=enemy.id;
                check(sim.Submit(cmd).accepted,prefix+"initial "+original+" accepted");
                var queued=Order(caster,CommandKind.Move,new Point(10.5f,24.5f));queued.sequence=2;queued.append=true;
                // Hold/Stop deliberately activate an appended order immediately, so
                // attach a future queue only while testing persistent active intents.
                bool queue=original==CommandKind.Move||original==CommandKind.AttackMove||original==CommandKind.Attack;
                if(queue)check(sim.Submit(queued).accepted,prefix+"queue retained behind "+original);
                check(Cast(sim,caster,damage.id,new Point(31.5f,20.5f)).accepted && caster.order==CommandKind.Cast,
                    prefix+"out-of-range cast temporarily interrupts "+original);
                float initial=caster.energy;int steps=0;while(caster.order==CommandKind.Cast&&steps++<300)sim.Step();
                check(caster.abilityCooldowns.Count==1 && caster.position.x>23 && caster.energy<initial,
                    prefix+"approaches and casts once from actual range and line of sight for "+original);
                check(caster.order==original && caster.activeOrderSequence==(original==CommandKind.Stop?0:1) && (!queue||caster.pendingOrders.Count==1&&caster.pendingOrders[0].sourceSequence==2),
                    prefix+"restores "+original+" and its pending waypoint");
            }

            {
                var sim=Fixture(catalog,faction);var caster=Add(sim,0,"caster",new Point(20.5f,20.5f));
                Add(sim,0,"scout",new Point(32.5f,18.5f));Warm(sim);
                Cast(sim,caster,damage.id,new Point(32.5f,20.5f));sim.Submit(Order(caster,CommandKind.Move,new Point(14.5f,20.5f)));Steps(sim,120);
                check(caster.abilityCooldowns.Count==0 && caster.energy==type.energyMax && caster.position.x<16,
                    prefix+"manual move cancels pending spell without spending or restoring old intent");
            }
            {
                var sim=Fixture(catalog,faction);var caster=Add(sim,0,"caster",new Point(20.5f,20.5f));
                Add(sim,0,"scout",new Point(32.5f,18.5f));Warm(sim);
                check(Cast(sim,caster,damage.id,new Point(32.5f,20.5f)).accepted,prefix+"Hold caster begins an approaching spell before appended movement");
                var first=Order(caster,CommandKind.Move,new Point(14.5f,20.5f));first.append=true;first.sequence=2;
                var second=Order(caster,CommandKind.Move,new Point(10.5f,20.5f));second.append=true;second.sequence=3;
                check(sim.Submit(first).accepted&&sim.Submit(second).accepted&&caster.order==CommandKind.Cast&&caster.pendingOrders.Count==2,
                    prefix+"append during cast preserves execution and collects future waypoints");
                int steps=0;while(caster.order==CommandKind.Cast&&steps++<300)sim.Step();
                check(caster.abilityCooldowns.Count==1&&caster.order==CommandKind.Move&&caster.activeOrderSequence==2
                    &&caster.pendingOrders.Count==1&&caster.pendingOrders[0].sourceSequence==3,
                    prefix+"completed cast from Hold activates the first appended order and preserves FIFO");
                Steps(sim,220);
                check(caster.position.Distance(second.position)<.2f&&caster.pendingOrders.Count==0,
                    prefix+"waypoints appended during cast actually finish instead of stranding under Hold");
            }
            {
                var sim=Fixture(catalog,faction);var caster=Add(sim,0,"caster",new Point(20.5f,20.5f));
                var scout=Add(sim,0,"scout",new Point(40.5f,20.5f));Warm(sim);
                check(Cast(sim,caster,damage.id,new Point(40.5f,20.5f)).accepted,prefix+"shared vision allows approach");
                sim.Entities.Remove(scout.id);Steps(sim,5);
                check(caster.order==CommandKind.Hold && caster.energy==type.energyMax && caster.abilityCooldowns.Count==0,
                    prefix+"loss of target vision cancels pending effect and restores prior order");
                caster.position=new Point(20.5f,20.5f);Warm(sim);Cast(sim,caster,damage.id,caster.position);caster.health=0;sim.Step();
                check(!sim.Entities.ContainsKey(caster.id)&&sim.View(0).abilityEvents.Length==0,prefix+"death cancels an accepted spell before execution");
            }
            {
                var sim=Fixture(catalog,faction);var caster=Add(sim,0,"caster",new Point(20.5f,20.5f));
                Add(sim,0,"scout",new Point(40.5f,20.5f));Warm(sim);
                // A newly placed impassable wall isolates the visible destination.
                for(int y=0;y<sim.Map.Height;y++)sim.Map.Occupy(new Point(27,y),1,9999);
                check(!Cast(sim,caster,damage.id,new Point(40.5f,20.5f)).accepted && caster.order==CommandKind.Hold && caster.energy==type.energyMax,
                    prefix+"unreachable target is rejected without consuming energy or intent");
            }
            {
                var sim=Fixture(catalog,faction);var caster=Add(sim,0,"caster",new Point(20.5f,20.5f));
                var hidden=Add(sim,1,"ranged",new Point(27.5f,20.5f));
                for(int y=18;y<=23;y++)sim.Map.Walkable[sim.Map.Index(26,y)]=false;
                Warm(sim);float health=hidden.health;
                check(sim.Visible(0,new Point(25.5f,20.5f))&&!sim.Visible(0,hidden.position),prefix+"fog fixture has a visible impact and hidden enemy within radius");
                check(Cast(sim,caster,damage.id,new Point(25.5f,20.5f)).accepted,prefix+"visible ground beside fog is a valid target");sim.Step();
                check(hidden.health==health&&sim.View(1).abilityEvents.Length==0,prefix+"area damage and event projection do not reveal or hit hidden enemies");
            }
            {
                var sim=Fixture(catalog,faction);var caster=Add(sim,0,"caster",new Point(20.5f,20.5f));
                var hiddenCaster=Add(sim,1,"caster",new Point(60.5f,20.5f));Warm(sim);
                Cast(sim,caster,damage.id,caster.position);sim.Step();
                string hiddenAbility=sim.Type(hiddenCaster).abilities[0];Cast(sim,hiddenCaster,hiddenAbility,hiddenCaster.position);sim.Step();
                Cast(sim,caster,heal.id,caster.position);sim.Step();
                check(sim.View(0).abilityEvents.Select(e=>e.id).SequenceEqual(new[]{1,2})&&sim.View(1).abilityEvents.Select(e=>e.id).SequenceEqual(new[]{1}),
                    prefix+"event identifiers expose no gaps from unseen enemy spells");
            }
            {
                var sim=Fixture(catalog,faction);var caster=Add(sim,0,"caster",new Point(20.5f,20.5f));Warm(sim);
                Cast(sim,caster,damage.id,caster.position);caster.energy=0;sim.Step();
                check(caster.order==CommandKind.Hold&&caster.abilityCooldowns.Count==0&&sim.View(0).abilityEvents.Length==0,
                    prefix+"execution revalidates energy before applying effect");
            }
        }
        CatalogValidation(catalog,check);
    }

    private static void Production(Catalog catalog,MapDefinition map,string faction,Action<bool,string> check)
    {
        var sim=new Simulation(map,catalog,faction);sim.Players[0].minerals=5000;sim.Players[0].catalyst=5000;
        var worker=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="worker");
        var academy=catalog.Role(faction,"academy");var caster=catalog.Role(faction,"caster");
        var position=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="hq").position;
        var request=new GameCommand{player=0,kind=CommandKind.Build,entities=new[]{worker.id},definition=academy.id,position=position};
        check(!sim.Submit(request).accepted,faction+" academy: prerequisite blocks premature construction");
        var tech=catalog.Role(faction,"tech");sim.Entities.Add(1000,new EntityState{id=1000,owner=0,definition=tech.id,position=new Point(40,40),health=tech.health});
        bool built=false;
        for(int y=0;y<sim.Map.Height&&!built;y++)for(int x=0;x<sim.Map.Width&&!built;x++)
        {request.position=new Point(x,y);built=sim.Submit(request).accepted;}
        check(built,faction+" academy: worker can construct the new building with technology");
        var producer=sim.Entities.Values.Single(e=>e.definition==academy.id&&e.owner==0);
        var train=new GameCommand{player=0,kind=CommandKind.Train,entities=new[]{producer.id},definition=caster.id};
        check(!sim.Submit(train).accepted,faction+" academy: foundation cannot produce casters");
        Steps(sim,academy.duration+400);
        check(producer.complete&&sim.Submit(train).accepted,faction+" academy: completed building accepts caster production");
        Steps(sim,caster.duration+20);
        var spawned=sim.Entities.Values.SingleOrDefault(e=>e.definition==caster.id&&e.owner==0);
        check(spawned!=null&&spawned.energy==caster.energyMax&&spawned.abilityCooldowns.Count==0,
            faction+" academy: actual trained caster deploys with full energy and ready abilities");
    }

    private static void CatalogValidation(Catalog catalog,Action<bool,string> check)
    {
        var json=new JsonSerializerOptions{IncludeFields=true};string original=JsonSerializer.Serialize(catalog.Definition,json);
        foreach(var invalid in new Action<CatalogDefinition>[] {
            c=>c.abilities[0].range=float.NaN,c=>c.abilities[0].amount=-1,c=>c.abilities[0].cooldown=0,
            c=>c.abilities[0].effect="unknown",c=>c.abilities[0].faction="unknown",
            c=>c.archetypes.First(t=>t.role=="caster").energyRegen=float.PositiveInfinity,
            c=>c.archetypes.First(t=>t.role=="caster").energyMax=1,
            c=>c.archetypes.First(t=>t.role=="caster").abilities=new[]{"missing"},
            c=>c.archetypes.First(t=>t.role=="worker").abilities=new[]{c.abilities[0].id},
            c=>{c.archetypes=c.archetypes.Where(t=>t.role!="caster").ToArray();c.abilities=Array.Empty<AbilityDefinition>();},
            c=>{c.archetypes=c.archetypes.Where(t=>t.role!="academy").ToArray();foreach(var t in c.archetypes.Where(t=>t.role=="caster")){t.producer="tech";t.requires="tech";}}})
        {
            var definition=JsonSerializer.Deserialize<CatalogDefinition>(original,json);invalid(definition);bool rejected=false;
            try{new Catalog(definition);}catch(ArgumentException){rejected=true;}
            check(rejected,"caster catalog rejects invalid values, ownership and non-caster references");
        }
        check(catalog.Ability(null)==null&&catalog.Ability("missing")==null,"unknown ability lookup is safely nullable");
    }
    private static bool Near(float a,float b)=>Math.Abs(a-b)<.02f;
    private static void Steps(Simulation sim,int count){for(int i=0;i<count;i++)sim.Step();}
    private static void Warm(Simulation sim)=>Steps(sim,4);
    private static GameCommand Order(EntityState unit,CommandKind kind,Point point=default)=>new GameCommand{player=unit.owner,kind=kind,entities=new[]{unit.id},position=point};
    private static CommandResult Cast(Simulation sim,EntityState caster,string ability,Point point)=>sim.Submit(new GameCommand{player=caster.owner,kind=CommandKind.Cast,entities=new[]{caster.id},definition=ability,position=point});
    private static EntityState Add(Simulation sim,int owner,string role,Point point)
    {
        var type=sim.Catalog.Role(sim.Players[owner].faction,role);var unit=new EntityState{id=sim.Entities.Keys.Max()+1,owner=owner,definition=type.id,
            position=point,health=type.health,energy=type.energyMax,order=CommandKind.Hold,attackReadyTick=int.MaxValue,lastDamageTick=sim.Tick};
        sim.Entities.Add(unit.id,unit);if(type.building)sim.Map.Occupy(point,type.footprint,unit.id);return unit;
    }
    private static Simulation Fixture(Catalog catalog,string faction)
    {
        var map=new MapDefinition{id="caster-checks",title="Caster checks",width=80,height=48,
            spawns=new[]{new SpawnDefinition{player=0,x=2,y=2},new SpawnDefinition{player=1,x=72,y=40}},
            resources=new[]{new ResourceDefinition{id="a",x=10,y=12},new ResourceDefinition{id="b",x=69,y=35},
                new ResourceDefinition{id="c",kind="catalyst",x=8,y=8},new ResourceDefinition{id="d",kind="catalyst",x=74,y=35}}};
        var sim=new Simulation(map,catalog,faction,faction=="industrial"?"biological":"industrial");
        foreach(var unit in sim.Entities.Values.Where(e=>!sim.Type(e).building).ToArray())sim.Entities.Remove(unit.id);return sim;
    }
}
