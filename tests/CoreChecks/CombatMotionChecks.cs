using Frontier.Core;

static class CombatMotionChecks
{
    public static void Run(Catalog catalog, Action<bool,string> check)
    {
        foreach(string faction in new[]{"industrial","biological"})
        {
            foreach(string role in new[]{"ranged","siege"})
            foreach(CommandKind order in new[]{CommandKind.Attack,CommandKind.AttackMove,CommandKind.Stop})
            {
                var sim=Fixture(catalog,faction);
                var attacker=Unit(sim,0,role,new Point(20.5f,20.5f));
                var type=sim.Type(attacker);
                var target=Unit(sim,1,"worker",new Point(attacker.position.x+type.range+1.5f,attacker.position.y));
                target.order=CommandKind.Hold;
                // Reveal the target before sending an explicit attack. Stop exercises
                // automatic pursuit, rather than the separate explicit Stop brake.
                for(int tick=0;tick<4;tick++)sim.Step();
                if(order!=CommandKind.Stop)
                {
                    var command=new GameCommand{player=0,kind=order,entities=new[]{attacker.id},target=target.id,position=new Point(60.5f,20.5f)};
                    if(!sim.Submit(command).accepted)throw new Exception("Combat motion fixture rejected "+order);
                }
                int attempts=0;
                while(attacker.position.Distance(target.position)>type.range&&attempts++<120)sim.Step();
                string label=faction+" "+role+" "+(order==CommandKind.Stop?"automatic pursuit":order.ToString());
                Point incoming=attacker.velocity;
                float brakingStep=type.deceleration/Simulation.TickRate;
                check(attacker.position.Distance(target.position)<=type.range&&incoming.Distance(default)>brakingStep+.01f,
                    label+": approaches weapon range with real momentum");
                Point before=attacker.position;float health=target.health;
                sim.Step();
                check(attacker.position.x>before.x+.0001f&&attacker.velocity.Distance(incoming)<=brakingStep+.005f
                    &&attacker.velocity.Distance(default)<incoming.Distance(default)-.001f,
                    label+": entering range brakes without deleting velocity");
                check(target.health<health&&attacker.order==order,
                    label+": braking preserves the combat order and permits an in-range shot");
                bool smooth=true;
                for(int tick=0;tick<15&&attacker.velocity.Distance(default)>.001f;tick++)
                {
                    incoming=attacker.velocity;sim.Step();
                    if(attacker.velocity.Distance(incoming)>brakingStep+.005f)smooth=false;
                }
                check(smooth&&attacker.velocity.Distance(default)<.001f,
                    label+": continues braking through the weapon cooldown until stopped");
            }

            // Moving away from a nearly out-of-range target must not create a shot
            // from a stale position after the braking displacement.
            {
                var sim=Fixture(catalog,faction);
                var attacker=Unit(sim,0,"siege",new Point(30.5f,20.5f));
                var type=sim.Type(attacker);
                var target=Unit(sim,1,"worker",new Point(attacker.position.x-type.range+.01f,attacker.position.y));
                WarmHeld(sim,attacker,target);
                attacker.velocity=new Point(type.speed,0);
                Attack(sim,attacker,target);float health=target.health;
                sim.Step();
                check(attacker.position.Distance(target.position)>type.range&&target.health==health,
                    faction+": braking out of range suppresses the pending shot");
                check(attacker.order==CommandKind.Attack&&attacker.target==target.id,
                    faction+": losing range during braking keeps the attack ready to pursue");
            }

            // Cross onto the upper end of a ramp while braking sideways. The target
            // stays in weapon range, but the new lower cell cannot see its plateau.
            {
                var sim=Fixture(catalog,faction);
                for(int y=18;y<=28;y++)for(int x=19;x<=23;x++)
                {
                    int cell=sim.Map.Index(x,y);sim.Map.Elevation[cell]=1;sim.Map.SurfaceLevel[cell]=1;
                    if(x==21){sim.Map.Elevation[cell]=0;sim.Map.SurfaceLevel[cell]=.9f;sim.Map.Ramp[cell]=true;}
                }
                var attacker=Unit(sim,0,"siege",new Point(20.99f,20.5f));
                var target=Unit(sim,1,"worker",new Point(20.99f,25.5f));
                WarmHeld(sim,attacker,target);
                check(sim.Map.HasLineOfSight(attacker.position,target.position),faction+": braking LOS fixture starts with a visible target");
                attacker.velocity=new Point(sim.Type(attacker).speed,0);
                Attack(sim,attacker,target);float health=target.health;
                sim.Step();
                check(sim.Map.CanStand(attacker.position,sim.Type(attacker).collisionRadius)
                    &&attacker.position.Distance(target.position)<sim.Type(attacker).range
                    &&!sim.Map.HasLineOfSight(attacker.position,target.position)&&target.health==health,
                    faction+": braking behind terrain rechecks line of sight before firing");
            }

            {
                var sim=Fixture(catalog,faction);
                var held=Unit(sim,0,"siege",new Point(20.5f,20.5f));
                var target=Unit(sim,1,"worker",new Point(26.5f,20.5f));
                WarmHeld(sim,held,target);
                held.velocity=new Point(sim.Type(held).speed,0);
                Point position=held.position;float health=target.health;
                sim.Step();
                check(held.position.Distance(position)<.001f&&held.order==CommandKind.Hold&&target.health<health,
                    faction+": Hold Position remains anchored while firing despite previous momentum");
            }

            {
                var sim=Fixture(catalog,faction);
                var tower=Unit(sim,0,"defense",new Point(20,20));
                sim.Map.Occupy(tower.position,sim.Type(tower).footprint,tower.id);
                var center=sim.Center(tower);
                var target=Unit(sim,1,"worker",new Point(center.x+6,center.y));
                WarmHeld(sim,tower,target);tower.velocity=new Point(2,0);
                Point position=tower.position;float health=target.health;
                sim.Step();
                check(tower.position.Distance(position)<.001f&&target.health<health,
                    faction+": defensive buildings fire without participating in movement braking");
            }
        }
    }

    private static void WarmHeld(Simulation sim,EntityState attacker,EntityState target)
    {
        attacker.order=CommandKind.Hold;attacker.attackReadyTick=int.MaxValue;target.order=CommandKind.Hold;
        for(int tick=0;tick<4;tick++)sim.Step();
        attacker.attackReadyTick=0;
    }
    private static void Attack(Simulation sim,EntityState attacker,EntityState target)
    {
        if(!sim.Submit(new GameCommand{player=0,kind=CommandKind.Attack,entities=new[]{attacker.id},target=target.id}).accepted)
            throw new Exception("Combat motion fixture rejected its visible target.");
    }
    private static EntityState Unit(Simulation sim,int owner,string role,Point position)
    {
        var type=sim.Catalog.Role(sim.Players[owner].faction,role);
        var unit=new EntityState{id=sim.Entities.Keys.Max()+1,owner=owner,definition=type.id,position=position,health=type.health};
        sim.Entities.Add(unit.id,unit);return unit;
    }
    private static Simulation Fixture(Catalog catalog,string faction)
    {
        var map=new MapDefinition
        {
            id="combat-motion",title="Combat motion regression",width=80,height=48,
            spawns=new[]{new SpawnDefinition{player=0,x=2,y=2},new SpawnDefinition{player=1,x=72,y=40}},
            resources=new[]{new ResourceDefinition{id="a",x=10,y=12,amount=10000},new ResourceDefinition{id="b",x=69,y=35,amount=10000},
                new ResourceDefinition{id="c",kind="catalyst",x=8,y=8},new ResourceDefinition{id="d",kind="catalyst",x=74,y=35}}
        };
        var sim=new Simulation(map,catalog,faction,faction);
        foreach(var unit in sim.Entities.Values.Where(e=>!sim.Type(e).building).ToArray())sim.Entities.Remove(unit.id);
        return sim;
    }
}
