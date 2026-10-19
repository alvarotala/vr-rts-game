using System.Reflection;
using System.Text.Json;
using Frontier.Core;

static class FactionParityChecks
{
    private static readonly string[] Factions={"industrial","biological"};
    private static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};

    public static void Run(Catalog catalog,MapDefinition[] maps,Action<bool,string> check)
    {
        Definitions(catalog,check);
        foreach(string faction in Factions)
        {
            Construction(catalog,faction,false,check);
            Construction(catalog,faction,true,check);
            Recovery(catalog,faction,check);
        }
        foreach(var map in maps)StartingEconomy(catalog,map,check);
        Training(catalog,check);
        MovementAndCombat(catalog,check);
        Spells(catalog,check);
    }

    private static void Definitions(Catalog catalog,Action<bool,string> check)
    {
        var first=catalog.Faction(Factions[0]);var second=catalog.Faction(Factions[1]);
        check(!first.grows&&!second.grows&&!first.regenerates&&!second.regenerates,
            "Faction parity: both factions require an active builder and have no passive health regeneration");
        CompareFields(first,second,new[]{"id","name"},"faction rules",check);
        var roles=catalog.Definition.archetypes.Where(t=>t.faction==Factions[0]).Select(t=>t.role).OrderBy(r=>r).ToArray();
        check(roles.SequenceEqual(catalog.Definition.archetypes.Where(t=>t.faction==Factions[1]).Select(t=>t.role).OrderBy(r=>r)),
            "Faction parity: each faction exposes exactly the same roles");
        foreach(string role in roles)
        {
            var a=catalog.Role(Factions[0],role);var b=catalog.Role(Factions[1],role);
            // Automatically includes new gameplay fields, so a future faction
            // difference must explicitly retire or revise this parity contract.
            CompareFields(a,b,new[]{"id","faction","name","abilities"},role+" gameplay",check);
            var left=a.abilities.Select(catalog.Ability).ToArray();var right=b.abilities.Select(catalog.Ability).ToArray();
            check(left.Select(x=>x.effect).SequenceEqual(right.Select(x=>x.effect)),"Faction parity: same ability order and effects for "+role);
            for(int i=0;i<Math.Min(left.Length,right.Length);i++)
                CompareFields(left[i],right[i],new[]{"id","faction","name","description"},role+" "+left[i].effect+" ability",check);
        }
    }

    private static void CompareFields<T>(T a,T b,string[] cosmetic,string label,Action<bool,string> check)
    {
        var mismatches=typeof(T).GetFields(BindingFlags.Public|BindingFlags.Instance)
            .Where(f=>!cosmetic.Contains(f.Name)&&!Equals(f.GetValue(a),f.GetValue(b))).Select(f=>f.Name).ToArray();
        check(mismatches.Length==0,"Faction parity: identical "+label+(mismatches.Length==0?"":"; differs: "+string.Join(", ",mismatches)));
    }

    private static void Construction(Catalog catalog,string faction,bool killBuilder,Action<bool,string> check)
    {
        string label="Faction parity construction "+faction+"/"+(killBuilder?"death":"move");
        var sim=Fixture(catalog,faction);var worker=Add(sim,0,"worker",new Point(18.5f,20.5f));Steps(sim,4);
        var supply=catalog.Role(faction,"supply");sim.Players[0].minerals=1000;
        check(sim.Submit(Order(worker,CommandKind.Build,new Point(20,20),definition:supply.id)).accepted,label+": real foundation accepted");
        var foundation=sim.Entities.Values.Single(e=>e.owner==0&&sim.Type(e).role=="supply");
        int paid=sim.Players[0].minerals;
        check(Until(sim,()=>foundation.remaining<supply.duration-10)&&worker.order==CommandKind.Build&&!foundation.complete,
            label+": building progresses while its worker stays assigned");
        int remaining=foundation.remaining;float health=foundation.health;
        if(killBuilder)worker.health=0;
        else check(sim.Submit(Order(worker,CommandKind.Move,new Point(35.5f,20.5f))).accepted,label+": builder can be reassigned");
        Steps(sim,supply.duration+20);
        check(!foundation.complete&&foundation.remaining==remaining&&foundation.health==health,
            label+": abandoned foundation never advances or gains construction health");
        check(killBuilder?!sim.Entities.ContainsKey(worker.id):worker.position.Distance(new Point(35.5f,20.5f))<.2f,
            label+": interruption actually removes or moves the worker");
        var replacement=Add(sim,0,"worker",new Point(17.5f,20.5f));
        check(sim.Submit(Order(replacement,CommandKind.Repair,target:foundation.id)).accepted&&replacement.order==CommandKind.Build,
            label+": replacement worker resumes the existing foundation");
        check(Until(sim,()=>foundation.complete,remaining+300)&&Math.Abs(foundation.health-supply.health)<.01f&&sim.Players[0].minerals==paid,
            label+": replacement completes construction without paying again (health="+foundation.health+", minerals="+sim.Players[0].minerals+")");
        check(Until(sim,()=>replacement.order!=CommandKind.Build,20),label+": worker releases only after completion");
    }

    private static void Recovery(Catalog catalog,string faction,Action<bool,string> check)
    {
        var sim=Fixture(catalog,faction);
        var worker=Add(sim,0,"worker",new Point(19.5f,20.5f));
        var army=Add(sim,0,"ranged",new Point(21.5f,20.5f));
        var structure=Add(sim,0,"supply",new Point(21,23));
        army.health-=24;structure.health-=24;float armyHealth=army.health,buildingHealth=structure.health;
        Steps(sim,400);
        check(army.health==armyHealth&&structure.health==buildingHealth,
            "Faction parity recovery "+faction+": injured troops and buildings do not regenerate when idle");
        foreach(var target in new[]{army,structure})
        {
            sim.Players[0].minerals=100;int initial=sim.Players[0].minerals;
            check(sim.Submit(Order(worker,CommandKind.Repair,target:target.id)).accepted,
                "Faction parity recovery "+faction+": worker accepts repair of "+sim.Type(target).role);
            check(Until(sim,()=>target.health==sim.Type(target).health,240)&&sim.Players[0].minerals==initial-6,
                "Faction parity recovery "+faction+": actual repair restores 24 health for 6 minerals on "+sim.Type(target).role);
        }
    }

    private static void StartingEconomy(Catalog catalog,MapDefinition map,Action<bool,string> check)
    {
        var a=new Simulation(map,catalog,Factions[0],Factions[1]);var b=new Simulation(map,catalog,Factions[1],Factions[0]);
        foreach(int player in new[]{0,1})
        {
            var hq=a.Entities.Values.Single(e=>e.owner==player&&a.Type(e).role=="hq");
            var command=Order(hq,CommandKind.Train,definition:catalog.Role(a.Players[player].faction,"worker").id);
            SubmitPair(a,b,command,check,"starting worker production "+map.id+"/"+player);
        }
        EqualSteps(a,b,800,check,"starting income, worker production and harvesting on "+map.id);
        check(a.Players.All(p=>p.minerals>500)&&a.Players.All(p=>a.Units(p.id)==7),
            "Faction parity: actual income and trained workers advance on "+map.id);
    }

    private static void Training(Catalog catalog,Action<bool,string> check)
    {
        var a=Fixture(catalog,Factions[0]);var b=Fixture(catalog,Factions[1]);
        foreach(var sim in new[]{a,b})foreach(int player in new[]{0,1})
        {
            sim.Players[player].minerals=5000;sim.Players[player].catalyst=5000;
            int x=player==0?10:55;
            Add(sim,player,"barracks",new Point(x,6));Add(sim,player,"tech",new Point(x,12));Add(sim,player,"academy",new Point(x,18));
        }
        foreach(int player in new[]{0,1})foreach(string role in new[]{"worker","scout","melee","ranged","siege","caster"})
        {
            var type=catalog.Role(a.Players[player].faction,role);
            var producer=a.Entities.Values.Single(e=>e.owner==player&&a.Type(e).role==type.producer);
            SubmitPair(a,b,Order(producer,CommandKind.Train,definition:type.id),check,"training "+role+"/"+player);
        }
        EqualSteps(a,b,1000,check,"all unit costs, training times, deployment and starting energy");
        check(a.Players.All(p=>a.Entities.Values.Count(e=>e.owner==p.id&&!a.Type(e).building)==6)&&a.Reserved(0)+a.Reserved(1)==0,
            "Faction parity: every requested unit actually finishes training for both players");
    }

    private static void MovementAndCombat(Catalog catalog,Action<bool,string> check)
    {
        var a=Fixture(catalog,Factions[0]);var b=Fixture(catalog,Factions[1]);
        string[] roles={"worker","scout","melee","ranged","siege","caster"};
        foreach(var sim in new[]{a,b})foreach(int player in new[]{0,1})for(int i=0;i<roles.Length;i++)
            Add(sim,player,roles[i],new Point(player==0?16.5f:58.5f,23.5f+i*2));
        foreach(int player in new[]{0,1})
        {
            var ids=a.Entities.Values.Where(e=>e.owner==player&&!a.Type(e).building).Select(e=>e.id).ToArray();
            SubmitPair(a,b,new GameCommand{player=player,kind=CommandKind.Move,entities=ids,position=new Point(player==0?29.5f:45.5f,30.5f)},check,"mixed movement "+player);
        }
        EqualSteps(a,b,400,check,"mixed-unit acceleration, speed, collision and formation arrival");
        check(a.Entities.Values.Where(e=>!a.Type(e).building).All(e=>e.order==CommandKind.Stop),"Faction parity: both moving armies arrive");
        foreach(var sim in new[]{a,b})foreach(var e in sim.Entities.Values)e.attackReadyTick=0;
        foreach(int player in new[]{0,1})
        {
            var ids=a.Entities.Values.Where(e=>e.owner==player&&!a.Type(e).building).Select(e=>e.id).ToArray();
            SubmitPair(a,b,new GameCommand{player=player,kind=CommandKind.AttackMove,entities=ids,position=new Point(player==0?52.5f:22.5f,30.5f)},check,"mixed attack "+player);
        }
        EqualSteps(a,b,800,check,"combat target choice, cooldowns, damage, splash and deaths");
        check(a.Units(0)+a.Units(1)<12,"Faction parity: the equal combat trace contains actual casualties");
    }

    private static void Spells(Catalog catalog,Action<bool,string> check)
    {
        var a=Fixture(catalog,Factions[0]);var b=Fixture(catalog,Factions[1]);
        foreach(var sim in new[]{a,b})
        {
            Add(sim,0,"caster",new Point(20.5f,20.5f));
            Add(sim,0,"melee",new Point(24.5f,19.5f)).health-=50;
            Add(sim,1,"melee",new Point(25.5f,21.5f));Add(sim,1,"ranged",new Point(26.5f,21.5f));
            Steps(sim,4);
        }
        var caster=a.Entities.Values.Single(e=>a.Type(e).role=="caster");
        foreach(string effect in new[]{"damage","heal"})
        {
            var ability=catalog.Ability(a.Type(caster).abilities.Single(id=>catalog.Ability(id).effect==effect));
            SubmitPair(a,b,Order(caster,CommandKind.Cast,new Point(25.5f,20.5f),definition:ability.id),check,effect+" spell");
            EqualSteps(a,b,20,check,effect+" spell targets, health, energy and cooldowns");
        }
        check(caster.abilityCooldowns.Count==2&&a.Entities.Values.Single(e=>e.owner==0&&a.Type(e).role=="melee").health>catalog.Role(Factions[0],"melee").health-50,
            "Faction parity: both damage and healing actually execute");
    }

    private static void SubmitPair(Simulation a,Simulation b,GameCommand command,Action<bool,string> check,string label)
    {
        var counterpart=new GameCommand{kind=command.kind,player=command.player,entities=command.entities.ToArray(),position=command.position,target=command.target,definition=command.definition};
        if(a.Catalog.Has(command.definition))counterpart.definition=b.Catalog.Role(b.Players[command.player].faction,a.Catalog[command.definition].role).id;
        else if(a.Catalog.Ability(command.definition)!=null)
        {
            string effect=a.Catalog.Ability(command.definition).effect;
            counterpart.definition=b.Catalog.Role(b.Players[command.player].faction,"caster").abilities.Single(id=>b.Catalog.Ability(id).effect==effect);
        }
        check(a.Submit(command).accepted&&b.Submit(counterpart).accepted,"Faction parity: paired orders accepted for "+label);
    }

    private static void EqualSteps(Simulation a,Simulation b,int ticks,Action<bool,string> check,string label)
    {
        int different=-1;
        if(Snapshot(a)!=Snapshot(b))different=a.Tick;
        for(int i=0;i<ticks&&different<0;i++)
        {
            a.Step();b.Step();if(Snapshot(a)!=Snapshot(b))different=a.Tick;
        }
        check(different<0,"Faction parity: exact same-slot faction-swap trace for "+label+(different<0?"":"; first difference at tick "+different));
    }

    private static string Snapshot(Simulation sim)
    {
        string result=JsonSerializer.Serialize(new{sim.Tick,sim.Winner,sim.Players,entities=sim.Entities.Values.OrderBy(e=>e.id).ToArray(),sim.Resources},Json);
        foreach(var type in sim.Catalog.Definition.archetypes)result=result.Replace("\""+type.id+"\"","\"role:"+type.role+"\"");
        foreach(var ability in sim.Catalog.Definition.abilities)result=result.Replace("\""+ability.id+"\"","\"ability:"+ability.effect+"\"");
        foreach(string faction in Factions)result=result.Replace("\""+faction+"\"","\"faction\"");
        return result;
    }

    private static Simulation Fixture(Catalog catalog,string first)
    {
        var map=new MapDefinition{id="faction-parity",title="Faction parity",width=80,height=64,
            spawns=new[]{new SpawnDefinition{player=0,x=2,y=2},new SpawnDefinition{player=1,x=72,y=56}},
            resources=new[]{new ResourceDefinition{id="left",x=10,y=55},new ResourceDefinition{id="right",x=69,y=8},
                new ResourceDefinition{id="left-gas",kind="catalyst",x=12,y=55},new ResourceDefinition{id="right-gas",kind="catalyst",x=67,y=8}}};
        var sim=new Simulation(map,catalog,first,first==Factions[0]?Factions[1]:Factions[0]);
        foreach(var unit in sim.Entities.Values.Where(e=>!sim.Type(e).building).ToArray())sim.Entities.Remove(unit.id);
        return sim;
    }
    private static EntityState Add(Simulation sim,int owner,string role,Point point)
    {
        var type=sim.Catalog.Role(sim.Players[owner].faction,role);
        var entity=new EntityState{id=Math.Max(1000,sim.Entities.Keys.Max()+1),owner=owner,definition=type.id,position=point,health=type.health,energy=type.energyMax,order=CommandKind.Hold,attackReadyTick=int.MaxValue};
        sim.Entities.Add(entity.id,entity);if(type.building)sim.Map.Occupy(point,type.footprint,entity.id);return entity;
    }
    private static GameCommand Order(EntityState entity,CommandKind kind,Point point=default,int target=0,string definition=null)
        =>new GameCommand{player=entity.owner,entities=new[]{entity.id},kind=kind,position=point,target=target,definition=definition};
    private static void Steps(Simulation sim,int ticks){for(int i=0;i<ticks;i++)sim.Step();}
    private static bool Until(Simulation sim,Func<bool> done,int limit=1000){for(int i=0;i<limit&&!done();i++)sim.Step();return done();}
}
