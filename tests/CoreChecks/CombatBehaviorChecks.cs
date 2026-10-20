using Frontier.Core;

static class CombatBehaviorChecks
{
    public static void Run(Catalog catalog, Action<bool,string> check)
    {
        foreach (string faction in new[] { "industrial", "biological" })
        {
            foreach (int owner in new[] { 0, 1 })
            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,owner,"melee",new Point(20.5f,20.5f));
                var retreating = Unit(sim,1-owner,"ranged",new Point(25.5f,20.5f));
                Warm(sim); Order(sim,soldier,CommandKind.AttackMove,new Point(40.5f,20.5f)); sim.Step();
                var nearby = Unit(sim,1-owner,"melee",new Point(23.5f,22.5f));
                retreating.position = new Point(28.5f,20.5f);
                Steps(sim,4);
                check(soldier.combatTarget == nearby.id,
                    faction + " player " + owner + ": automatic pursuit can leave a retreating target for a substantially closer threat");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldiers = Enumerable.Range(0,6).Select(i=>Unit(sim,0,"ranged",new Point(15.5f+i,20.5f))).ToArray();
                var enemy = Unit(sim,1,"ranged",new Point(28.5f,20.5f));
                Unit(sim,0,"worker",new Point(28.5f,24.5f));
                Warm(sim);
                if(!sim.Submit(new GameCommand{player=0,kind=CommandKind.AttackMove,entities=soldiers.Select(s=>s.id).ToArray(),position=new Point(40.5f,20.5f)}).accepted)
                    throw new Exception("Firing position fixture rejected");
                sim.Step();
                var approaching = soldiers.Where(s=>s.approachTarget == enemy.id).ToArray();
                check(approaching.Length >= 4 && approaching.All(a=>approaching.All(b=>a==b
                    || a.approachGoal.Distance(b.approachGoal) >= sim.Type(a).collisionRadius+sim.Type(b).collisionRadius)),
                    faction + ": approaching soldiers choose separate firing positions instead of stacking on one arrival cell ("
                        + string.Join("; ",approaching.Select(a=>$"{a.id}: {a.approachGoal.x},{a.approachGoal.y}")) + ")");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"ranged",new Point(20.5f,20.5f));
                var enemy = Unit(sim,1,"ranged",new Point(29.5f,20.5f));
                Warm(sim); Order(sim,soldier,CommandKind.AttackMove,new Point(42.5f,20.5f)); sim.Step();
                var blocker = Unit(sim,0,"melee",soldier.approachGoal);
                var heldPosition = blocker.position;
                for(int i=0;i<160 && enemy.health==sim.Type(enemy).health;i++)sim.Step();
                check(enemy.health < sim.Type(enemy).health && blocker.position.Distance(heldPosition)<.001f,
                    faction + ": a newly occupied firing position is replaced without moving a Hold ally");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"ranged",new Point(20.5f,20.5f));
                var worker = Unit(sim,1,"worker",new Point(22.5f,20.5f));
                var distant = Unit(sim,1,"ranged",new Point(28.5f,20.5f));
                Warm(sim); Order(sim,soldier,CommandKind.AttackMove,new Point(40.5f,20.5f)); sim.Step();
                check(worker.health < sim.Type(worker).health && distant.health == sim.Type(distant).health,
                    faction + ": automatic combat takes an available shot before chasing a distant threat");
            }

            foreach (int owner in new[] { 0, 1 })
            {
                var sim = Fixture(catalog,faction);
                var soldiers = Enumerable.Range(0,9).Select(i=>Unit(sim,owner,"melee",new Point(20.5f-i/3,19.5f+i%3))).ToArray();
                var nearest = Unit(sim,1-owner,"ranged",new Point(24.5f,20.5f));
                var adjacent = Unit(sim,1-owner,"ranged",new Point(25.5f,23.5f));
                Warm(sim);
                if(!sim.Submit(new GameCommand{player=owner,kind=CommandKind.AttackMove,entities=soldiers.Select(s=>s.id).ToArray(),position=new Point(35.5f,20.5f)}).accepted)
                    throw new Exception("Crowded target fixture rejected");
                foreach(var soldier in soldiers)soldier.attackReadyTick=0;
                sim.Step();
                check(soldiers.Any(s=>s.combatTarget==nearest.id) && soldiers.Any(s=>s.combatTarget==adjacent.id),
                    faction + " player " + owner + ": an approaching melee group engages adjacent threats instead of everyone pursuing the nearest enemy");
                var destination = soldiers.Select(s=>s.destination).ToArray();
                var fired = new HashSet<int>();
                for(int tick=0;tick<180;tick++)
                {sim.Step();foreach(var soldier in soldiers)if(soldier.lastShotTick==sim.Tick)fired.Add(soldier.id);}
                check(nearest.health <= 0 && adjacent.health <= 0 && fired.Count >= 4
                    && soldiers.Select((s,i)=>s.destination.Distance(destination[i])<.001f).All(s=>s),
                    faction + " player " + owner + ": multiple melee fighters clear both threats and preserve their destinations without reorders (" + fired.Count + " participants)");
            }

            foreach(int size in new[]{18,Simulation.UnitLimit}) foreach(int owner in new[]{0,1})
            {
                var sim = Fixture(catalog,faction);
                var armies = new[]{new List<EntityState>(),new List<EntityState>()};
                var roles = new[]{"melee","ranged","siege","scout","caster"};
                int rows = size==Simulation.UnitLimit ? 10 : 6;
                for(int i=0;i<size;i++) foreach(int side in new[]{0,1})
                    armies[side].Add(Unit(sim,side,roles[i%roles.Length],
                        new Point(side==0?19.5f-i/rows*1.1f:31.5f+i/rows*1.1f,14.5f+i%rows*1.1f)));
                Warm(sim);
                if(!sim.Submit(new GameCommand{player=owner,kind=CommandKind.AttackMove,entities=armies[owner].Select(s=>s.id).ToArray(),
                    position=new Point(owner==0?31.5f:19.5f,20.5f)}).accepted)throw new Exception("Live engagement fixture rejected");
                foreach(var unit in armies.SelectMany(a=>a))unit.attackReadyTick=0;
                var fired = new[]{new HashSet<int>(),new HashSet<int>()}; bool overlap=false;
                for(int tick=0;tick<400;tick++)
                {
                    sim.Step();
                    foreach(var army in armies)foreach(var unit in army)
                    {
                        if(unit.lastShotTick==sim.Tick)fired[unit.owner].Add(unit.id);
                        if(unit.health<=0)continue;
                        if(army.Any(other=>other.id!=unit.id&&other.health>0&&unit.position.Distance(other.position)+.001f
                            <sim.Type(unit).collisionRadius+sim.Type(other).collisionRadius))overlap=true;
                    }
                }
                string label=faction+" "+size+" vs "+size+", attacker "+owner;
                check(fired.All(f=>f.Count>=size*.8f),label+": both live mixed armies participate from a single attack order, including defenders that shoot back");
                check(!overlap && armies[owner].All(s=>s.order==CommandKind.AttackMove||s.order==CommandKind.Stop),
                    label+": sustained combat preserves body separation and the player's order");
            }

            foreach(int groupSize in new[]{18,Simulation.UnitLimit})
            {
                var sim = Fixture(catalog,faction);
                var front = Unit(sim,0,"ranged",new Point(35.5f,20.5f));
                var rear = new List<EntityState>();
                for(int i=0;i<groupSize-1;i++)rear.Add(Unit(sim,0,i%3==0?"siege":i%3==1?"ranged":"melee",
                    new Point(13.5f+i%3*1.5f,14.5f+i/3*1.5f)));
                var focus = Unit(sim,1,"worker",new Point(40.5f,20.5f));
                var defenders = new List<EntityState>();
                for(int i=0;i<18;i++)defenders.Add(Unit(sim,1,"ranged",new Point(43.5f+i%3*1.5f,13.5f+i/3*2)));
                Warm(sim); focus.health=1;
                var army = rear.Prepend(front).ToArray();
                var command = new GameCommand{player=0,kind=CommandKind.Attack,entities=army.Select(u=>u.id).ToArray(),target=focus.id};
                if(!sim.Submit(command).accepted)throw new Exception("Group focus fixture rejected");
                foreach(var unit in army)unit.attackReadyTick=0;
                Steps(sim,4);
                check(!sim.Entities.ContainsKey(focus.id) && rear.All(unit=>unit.order==CommandKind.AttackMove),
                    faction + " " + groupSize + "-unit group: rear soldiers keep advancing when the front kills the focused target");
                var fired = new HashSet<int>();
                for(int tick=0;tick<(groupSize==Simulation.UnitLimit?1200:500);tick++)
                {
                    sim.Step();
                    foreach(var shot in sim.View(0).attacks)if(shot.owner==0)fired.Add(shot.attacker);
                }
                check(rear.All(unit=>unit.position.x>32 || fired.Contains(unit.id)),
                    faction + " " + groupSize + "-unit group: the whole spread-out group joins the battle without individual reorders");
            }

            {
                var sim = Fixture(catalog,faction);
                var front = Unit(sim,0,"ranged",new Point(35.5f,20.5f));
                var rear = Unit(sim,0,"ranged",new Point(15.5f,20.5f));
                var focus = Unit(sim,1,"worker",new Point(40.5f,20.5f));
                Warm(sim); focus.health=1;
                if(!sim.Submit(new GameCommand{player=0,kind=CommandKind.Attack,entities=new[]{front.id,rear.id},target=focus.id}).accepted)
                    throw new Exception("Queued group focus fixture rejected");
                front.attackReadyTick=0;
                Order(sim,rear,CommandKind.Move,new Point(10.5f,20.5f),append:true,sequence:2);
                Steps(sim,4);
                check(rear.order==CommandKind.Move && rear.activeOrderSequence==2 && rear.destination.x<11,
                    faction + ": queued retreat overrides automatic continuation after a distant focused kill");
            }

            foreach (var order in new[] { CommandKind.Stop, CommandKind.Hold, CommandKind.AttackMove })
            {
                var sim = Fixture(catalog, faction);
                var soldier = Unit(sim, 0, "ranged", new Point(20.5f,20.5f));
                var building = Unit(sim, 1, "supply", new Point(23,20));
                var defender = Unit(sim, 1, "ranged", new Point(20.5f,25.5f));
                Warm(sim); Order(sim, soldier, order, new Point(45.5f,20.5f));
                sim.Step();
                check(defender.health < sim.Type(defender).health && building.health == sim.Type(building).health,
                    faction + " " + order + ": armed defenders take priority over a closer economic building");
            }

            {
                var sim = Fixture(catalog, faction);
                var soldier = Unit(sim, 0, "ranged", new Point(20.5f,20.5f));
                var building = Unit(sim, 1, "supply", new Point(23,20));
                Warm(sim); Order(sim, soldier, CommandKind.AttackMove, new Point(45.5f,20.5f)); sim.Step();
                check(building.health < sim.Type(building).health, faction + ": attack-move attacks an undefended building");
                var defender = Unit(sim, 1, "ranged", new Point(20.5f,25.5f));
                Steps(sim,4); float health = building.health; soldier.attackReadyTick = 0; sim.Step();
                check(defender.health < sim.Type(defender).health && building.health == health,
                    faction + ": an arriving defender interrupts an automatic building attack");
                check(soldier.order == CommandKind.AttackMove && soldier.destination.Distance(new Point(45.5f,20.5f)) < .01f,
                    faction + ": combat preserves the attack-move destination");
            }

            {
                var sim = Fixture(catalog, faction);
                var soldier = Unit(sim, 0, "ranged", new Point(20.5f,20.5f));
                var building = Unit(sim, 1, "supply", new Point(23,20));
                var defender = Unit(sim, 1, "ranged", new Point(20.5f,25.5f));
                Warm(sim); Order(sim,soldier,CommandKind.Attack,target:building.id); sim.Step();
                check(building.health < sim.Type(building).health && defender.health == sim.Type(defender).health,
                    faction + ": explicit focus fire overrides automatic threat priorities");
                building.health = 1; soldier.attackReadyTick = 0; sim.Step();
                soldier.attackReadyTick = 0; sim.Step();
                check(defender.health < sim.Type(defender).health,
                    faction + ": destroying a focused target immediately acquires the next threat");
            }

            {
                var sim = Fixture(catalog, faction);
                var soldier = Unit(sim,0,"ranged",new Point(20.5f,20.5f));
                var first = Unit(sim,1,"ranged",new Point(24.5f,20.5f));
                var second = Unit(sim,1,"ranged",new Point(20.5f,25.5f));
                Warm(sim); Order(sim,soldier,CommandKind.Hold); sim.Step();
                float health = first.health; second.position = new Point(20.5f,23.5f);
                soldier.attackReadyTick = 0; sim.Step();
                check(first.health < health && second.health == sim.Type(second).health,
                    faction + ": keeps a valid firing target when an equal threat moves closer");
                first.position = new Point(30.5f,20.5f); soldier.attackReadyTick = 0; sim.Step();
                check(second.health < sim.Type(second).health,
                    faction + ": switches to a threat in range instead of chasing a retreating target");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"ranged",new Point(20.5f,20.5f));
                var worker = Unit(sim,1,"worker",new Point(23.5f,20.5f));
                var tower = Unit(sim,1,"defense",new Point(24,24));
                Warm(sim); Order(sim,soldier,CommandKind.Hold); sim.Step();
                check(tower.health < sim.Type(tower).health && worker.health == sim.Type(worker).health,
                    faction + ": a completed armed tower is a threat, not an economic building");
                tower.complete = false; tower.remaining = sim.Type(tower).duration;
                soldier.attackReadyTick = 0; float health = tower.health; sim.Step();
                check(worker.health < sim.Type(worker).health && tower.health == health,
                    faction + ": unfinished towers do not outrank mobile targets");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"ranged",new Point(20.5f,20.5f));
                var enemy = Unit(sim,1,"worker",new Point(25.5f,20.5f));
                Warm(sim); Order(sim,soldier,CommandKind.AttackMove,new Point(38.5f,20.5f));
                Order(sim,soldier,CommandKind.Move,new Point(44.5f,20.5f),append:true,sequence:2);
                enemy.health = 1; sim.Step();
                check(soldier.order == CommandKind.AttackMove && soldier.pendingOrders.Count == 1,
                    faction + ": a kill does not discard attack-move or prematurely consume its queue");
                for(int i=0;i<700 && soldier.activeOrderSequence!=2;i++)sim.Step();
                check(soldier.activeOrderSequence == 2 && soldier.position.x > 38,
                    faction + ": resumes the attack-move route and then its queued waypoint");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"ranged",new Point(16.5f,20.5f));
                var first = Unit(sim,1,"melee",new Point(22.5f,20.5f));
                var second = Unit(sim,1,"ranged",new Point(34.5f,20.5f));
                var building = Unit(sim,1,"supply",new Point(45,20));
                first.health=second.health=building.health=1;
                Warm(sim);var destination=new Point(60.5f,20.5f);
                Order(sim,soldier,CommandKind.AttackMove,destination);
                var hits=new List<int>();
                for(int i=0;i<1000;i++)
                {
                    sim.Step();
                    foreach(var shot in sim.View(0).attacks.Where(a=>a.attacker==soldier.id))
                        if(!hits.Contains(shot.target))hits.Add(shot.target);
                }
                check(hits.SequenceEqual(new[]{first.id,second.id,building.id})
                    &&new[]{first,second,building}.All(e=>!sim.Entities.ContainsKey(e.id)),
                    faction + ": one attack-move fights successive encountered units and then a structure without new orders");
                check(soldier.order==CommandKind.Stop&&soldier.position.Distance(destination)<.2f,
                    faction + ": clearing successive enemies resumes the route all the way to the attack destination");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"ranged",new Point(20.5f,20.5f));
                var enemy = Unit(sim,1,"ranged",new Point(25.5f,20.5f));
                Warm(sim); Order(sim,soldier,CommandKind.Move,new Point(12.5f,20.5f)); Steps(sim,20);
                check(soldier.position.x < 19 && enemy.health == sim.Type(enemy).health,
                    faction + ": a retreat Move is never hijacked by automatic combat");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"ranged",new Point(20.5f,20.5f));
                var enemy = Unit(sim,1,"ranged",new Point(25.5f,20.5f));
                var ally = Unit(sim,0,"siege",new Point(18.5f,22.5f));
                Warm(sim); Order(sim,ally,CommandKind.Move,new Point(40.5f,22.5f));
                Order(sim,soldier,CommandKind.AttackMove,new Point(42.5f,20.5f));
                soldier.yieldUntil = sim.Tick + 200; soldier.yieldDeadline = sim.Tick + 400;
                soldier.yieldPriority = ally.trafficPriority; soldier.yieldGoal = new Point(18.5f,18.5f);
                sim.Step();
                check(enemy.health < sim.Type(enemy).health && soldier.order == CommandKind.AttackMove,
                    faction + ": giving way to an ally cannot suppress an available shot");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"melee",new Point(20.5f,20.5f));
                var trapped = Unit(sim,1,"worker",new Point(25.5f,20.5f));
                Unit(sim,0,"worker",new Point(27.5f,20.5f));
                for(int y=18;y<=22;y++)for(int x=23;x<=29;x++)
                    if(x<25 || x>27 || y!=20)sim.Map.Walkable[sim.Map.Index(x,y)]=false;
                Warm(sim); Order(sim,soldier,CommandKind.AttackMove,new Point(40.5f,30.5f));
                check(sim.Visible(0,trapped.position),faction + ": an allied spotter reveals the unreachable auto-target");
                sim.Step();
                check(soldier.order == CommandKind.AttackMove,
                    faction + ": an unreachable auto-target cannot cancel the attack-move order");
                Steps(sim,300);
                check(soldier.position.x > 32 && trapped.health == sim.Type(trapped).health,
                    faction + ": ignores an unreachable target and continues the original route");
            }

            {
                var sim = Fixture(catalog,faction);
                // A shooter below a plateau can see the target through an allied spotter,
                // but must walk around the lower edge to get its own line of fire.
                for(int y=18;y<=25;y++)for(int x=24;x<=26;x++)
                {int cell=sim.Map.Index(x,y);sim.Map.Elevation[cell]=1;sim.Map.SurfaceLevel[cell]=1;}
                var soldier = Unit(sim,0,"ranged",new Point(22.5f,20.5f));
                var enemy = Unit(sim,1,"worker",new Point(28.5f,20.5f));
                Unit(sim,0,"worker",new Point(29.5f,23.5f));
                Warm(sim); Order(sim,soldier,CommandKind.Attack,target:enemy.id);
                check(!sim.Map.HasLineOfSight(soldier.position,enemy.position),faction + ": terrain blocks the initial shot");
                for(int i=0;i<400 && enemy.health==sim.Type(enemy).health;i++)sim.Step();
                check(enemy.health < sim.Type(enemy).health && sim.Map.HasLineOfSight(soldier.position,enemy.position),
                    faction + ": seeks a firing position instead of stopping in range behind terrain");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"ranged",new Point(20.5f,20.5f));
                var enemy = Unit(sim,1,"worker",new Point(27.5f,20.5f));
                Warm(sim); Order(sim,soldier,CommandKind.Attack,target:enemy.id);
                enemy.position = new Point(60.5f,30.5f); Steps(sim,4);
                check(soldier.order == CommandKind.Attack && soldier.destination.x < 30,
                    faction + ": losing vision keeps the last known position without tracking through fog");
                Steps(sim,160);
                check(soldier.position.x < 32 && enemy.health == sim.Type(enemy).health,
                    faction + ": lost-target pursuit is bounded and never damages hidden enemies");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"ranged",new Point(20.5f,20.5f));
                var building = Unit(sim,1,"supply",new Point(23,20));
                Warm(sim); Order(sim,soldier,CommandKind.AttackMove,new Point(45.5f,20.5f)); sim.Step();
                int ready = soldier.attackReadyTick;
                var enemy = Unit(sim,1,"ranged",new Point(20.5f,25.5f));
                while(sim.Tick < ready-1)sim.Step();
                check(enemy.health == sim.Type(enemy).health && soldier.attackReadyTick == ready,
                    faction + ": retargeting does not reset or bypass the weapon cooldown");
                sim.Step();
                check(enemy.health < sim.Type(enemy).health,
                    faction + ": a new threat receives the next shot as soon as the cooldown ends");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"ranged",new Point(20.5f,20.5f));
                var building = Unit(sim,1,"supply",new Point(23,20));
                var worker = Unit(sim,1,"worker",new Point(20.5f,21.7f));
                Warm(sim); Order(sim,soldier,CommandKind.Hold);
                Order(sim,worker,CommandKind.Attack,target:soldier.id);
                Steps(sim,30);
                check(soldier.health < sim.Type(soldier).health && worker.health < sim.Type(worker).health
                    && building.health == sim.Type(building).health,
                    faction + ": armed workers fighting back are handled automatically");
            }

            {
                var sim = Fixture(catalog,faction);
                for(int y=18;y<=25;y++)for(int x=24;x<=26;x++)
                {int cell=sim.Map.Index(x,y);sim.Map.Elevation[cell]=1;sim.Map.SurfaceLevel[cell]=1;}
                var soldier = Unit(sim,0,"ranged",new Point(22.5f,20.5f));
                var screened = Unit(sim,1,"ranged",new Point(28.5f,20.5f));
                var exposed = Unit(sim,1,"worker",new Point(22.5f,25.5f));
                Unit(sim,0,"worker",new Point(29.5f,23.5f));
                Warm(sim); Order(sim,soldier,CommandKind.Hold); sim.Step();
                check(exposed.health < sim.Type(exposed).health && screened.health == sim.Type(screened).health
                    && soldier.position.Distance(new Point(22.5f,20.5f)) < .001f,
                    faction + ": Hold fires at an exposed enemy rather than getting stuck on a screened threat");
            }

            {
                var sim = Fixture(catalog,faction);
                var soldier = Unit(sim,0,"ranged",new Point(20.5f,20.5f));
                var focused = Unit(sim,1,"worker",new Point(27.5f,20.5f));
                var threat = Unit(sim,1,"ranged",new Point(20.5f,25.5f));
                Warm(sim); Order(sim,soldier,CommandKind.Attack,target:focused.id);
                focused.position = new Point(60.5f,30.5f); sim.Step();
                check(threat.health < sim.Type(threat).health && soldier.target == focused.id,
                    faction + ": temporarily losing the focused target does not silence nearby combat");
            }

            {
                var sim = Fixture(catalog,faction);
                var army = new List<EntityState>();
                var defenders = new List<EntityState>();
                for(int i=0;i<18;i++)
                {
                    army.Add(Unit(sim,0,i%3==0?"siege":i%3==1?"ranged":"melee",new Point(19.5f+i%3*1.5f,14.5f+i/3*1.5f)));
                    var defender=Unit(sim,1,"ranged",new Point(31.5f+i%3*1.5f,14.5f+i/3*1.5f));
                    defenders.Add(defender);
                }
                Warm(sim);
                var result=sim.Submit(new GameCommand{player=0,kind=CommandKind.AttackMove,entities=army.Select(u=>u.id).ToArray(),position=new Point(42.5f,19.5f)});
                if(!result.accepted)throw new Exception(result.message);
                foreach(var unit in army)unit.attackReadyTick=0;
                var fired = new HashSet<int>(); bool overlap=false;
                for(int tick=0;tick<600;tick++)
                {
                    sim.Step();
                    foreach(var shot in sim.View(0).attacks)if(shot.owner==0)fired.Add(shot.attacker);
                    for(int i=0;i<army.Count;i++)for(int j=i+1;j<army.Count;j++)
                        if(army[i].position.Distance(army[j].position)+.001f<sim.Type(army[i]).collisionRadius+sim.Type(army[j]).collisionRadius)overlap=true;
                }
                check(army.All(unit=>fired.Contains(unit.id)),
                    faction + ": all 18 mixed soldiers contribute to a battle from one group attack-move");
                bool cleared = defenders.All(unit=>!sim.Entities.ContainsKey(unit.id));
                check(!overlap && army.All(unit=>unit.order==CommandKind.AttackMove || (cleared && unit.order==CommandKind.Stop)),
                    faction + ": sustained group combat preserves collision separation and orders");
            }
        }
    }

    private static void Steps(Simulation sim,int count) {for(int i=0;i<count;i++)sim.Step();}
    private static void Warm(Simulation sim) {Steps(sim,4);}
    private static void Order(Simulation sim,EntityState unit,CommandKind kind,Point position=default,int target=0,bool append=false,int sequence=1)
    {
        // Zero sequences are allowed by the simulation for local fixture commands.
        var result=sim.Submit(new GameCommand{player=unit.owner,kind=kind,entities=new[]{unit.id},position=position,target=target,append=append,sequence=append?sequence:0});
        if(!result.accepted)throw new Exception("Combat fixture rejected "+kind+": "+result.message);
        if(!append)unit.attackReadyTick=0;
    }
    private static EntityState Unit(Simulation sim,int owner,string role,Point position)
    {
        var type=sim.Catalog.Role(sim.Players[owner].faction,role);
        var unit=new EntityState{id=sim.Entities.Keys.Max()+1,owner=owner,definition=type.id,position=position,
            health=type.health,order=CommandKind.Hold,attackReadyTick=int.MaxValue};
        sim.Entities.Add(unit.id,unit);
        if(type.building)sim.Map.Occupy(position,type.footprint,unit.id);
        return unit;
    }
    private static Simulation Fixture(Catalog catalog,string faction)
    {
        var map=new MapDefinition{id="combat-behavior",title="Combat behavior",width=80,height=48,
            spawns=new[]{new SpawnDefinition{player=0,x=2,y=2},new SpawnDefinition{player=1,x=72,y=40}},
            resources=new[]{new ResourceDefinition{id="a",x=10,y=12,amount=10000},new ResourceDefinition{id="b",x=69,y=35,amount=10000},
                new ResourceDefinition{id="c",kind="catalyst",x=8,y=8},new ResourceDefinition{id="d",kind="catalyst",x=74,y=35}}};
        var sim=new Simulation(map,catalog,faction,faction);
        foreach(var unit in sim.Entities.Values.Where(e=>!sim.Type(e).building).ToArray())sim.Entities.Remove(unit.id);
        return sim;
    }
}
