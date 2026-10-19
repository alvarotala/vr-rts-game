using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Strategist
    {
        private readonly HashSet<int> tacticalAttackers=new HashSet<int>();
        private readonly Dictionary<int,(CommandKind kind,Point point)> tacticalOrders=new Dictionary<int,(CommandKind,Point)>();
        private readonly Dictionary<int,(Point point,int started)> tacticalReturning=new Dictionary<int,(Point,int)>();
        private int tacticalNextAttack,tacticalStarted,tacticalTarget,tacticalScout,tacticalPatrol,tacticalPeak,tacticalTick;
        private Point tacticalDestination,tacticalScoutGoal;
        private bool tacticalActive;

        private IEnumerable<GameCommand> DecideTactics(PlayerView view,VisibleEntity[] own)
        {
            var commands=new List<GameCommand>();
            tacticalTick=view.tick;
            var bases=own.Where(e=>Type(e).role=="hq"&&e.health>0).OrderBy(e=>e.id).ToArray();
            if(bases.Length==0)return commands;
            var army=own.Where(e=>e.complete&&e.health>0&&!Type(e).building&&Type(e).role!="worker").ToArray();
            var enemies=view.entities.Where(e=>e.owner!=player&&!e.remembered&&e.health>0).ToArray();
            var live=new HashSet<int>(army.Select(e=>e.id));
            tacticalAttackers.RemoveWhere(id=>!live.Contains(id));
            foreach(int id in tacticalOrders.Keys.Where(id=>!live.Contains(id)).ToArray())tacticalOrders.Remove(id);
            foreach(int id in tacticalReturning.Keys.Where(id=>!live.Contains(id)).ToArray())tacticalReturning.Remove(id);
            var home=Center(bases[0]);

            // Retreat is a real Move until arrival. Hold is issued only after reaching
            // the safe formation, never immediately behind a Move in the same decision.
            foreach(var unit in army)
                if(!tacticalReturning.ContainsKey(unit.id)&&unit.health/Type(unit).health<pace.retreatHealth
                    &&enemies.Any(e=>ArmedThreat(e)&&Center(e).Distance(unit.position)<11))
                    Return(unit,SafeRally(own,bases,enemies,unit.position),view.tick);
            foreach(var unit in army.Where(e=>tacticalReturning.ContainsKey(e.id)))
            {
                var retreat=tacticalReturning[unit.id];
                if(!bases.Any(b=>Center(b).Distance(retreat.point)<15))
                {
                    retreat=(SafeRally(own,bases,enemies,unit.position),retreat.started);
                    tacticalReturning[unit.id]=retreat;tacticalOrders.Remove(unit.id);
                }
                bool arrived=unit.position.Distance(retreat.point)<=5
                    ||unit.order==CommandKind.Stop&&unit.position.Distance(retreat.point)<12;
                if(!arrived)Issue(commands,view,new[]{unit},CommandKind.Move,retreat.point);
                else
                {
                    if(unit.order!=CommandKind.Hold&&unit.order!=CommandKind.Cast)commands.Add(Command(CommandKind.Hold,unit.id));
                    if(view.tick-retreat.started>=Seconds(pace.waveRest)&&unit.health>=Type(unit).health*.5f)
                        tacticalReturning.Remove(unit.id);
                }
            }
            var unavailable=new HashSet<int>(tacticalReturning.Keys);
            var defenders=new HashSet<int>();
            // Each threatened base gets enough nearby defenders for the observed
            // force. A single scout no longer recalls the entire offensive army.
            foreach(var headquarters in bases.OrderBy(b=>b.health/Type(b).health))
            {
                var center=Center(headquarters);
                var intruders=enemies.Where(e=>Center(e).Distance(center)<14&&(!Type(e).building||Type(e).damage>0)).ToArray();
                if(intruders.Length==0)continue;
                var threat=intruders.OrderByDescending(CombatPower).ThenBy(e=>Center(e).Distance(center)).First();
                float needed=intruders.Sum(CombatPower)*(Difficulty==AiDifficulty.Easy?1.15f:1.35f)+3,power=0;
                var response=new List<VisibleEntity>();
                foreach(var unit in army.Where(e=>!unavailable.Contains(e.id)&&!defenders.Contains(e.id)&&e.order!=CommandKind.Cast)
                    .OrderBy(e=>e.position.Distance(center)+(tacticalAttackers.Contains(e.id)?8:0)).ThenBy(e=>e.id))
                {
                    response.Add(unit);power+=CombatPower(unit);defenders.Add(unit.id);tacticalAttackers.Remove(unit.id);
                    if(response.Count>=2&&power>=needed)break;
                }
                Issue(commands,view,response.ToArray(),CommandKind.AttackMove,Center(threat));
            }
            unavailable.UnionWith(defenders);

            var active=army.Where(e=>tacticalAttackers.Contains(e.id)&&!unavailable.Contains(e.id)).ToArray();
            if(tacticalActive&&active.Length==0)
            {tacticalActive=false;tacticalNextAttack=view.tick+Seconds(pace.waveRest);}
            if(tacticalActive&&active.Length>0)
            {
                var center=ArmyCenter(active);
                var opposition=enemies.Where(e=>ArmedThreat(e)&&Center(e).Distance(center)<13).ToArray();
                float power=active.Sum(CombatPower),enemyPower=opposition.Sum(CombatPower);
                bool losing=view.tick-tacticalStarted>Seconds(10)&&enemyPower>power*1.7f;
                bool broken=active.Length<Math.Max(2,tacticalPeak*.4f)&&enemyPower>power;
                // A stalled raid with no nearby combat regroups; an army actively
                // winning a battle is never recalled just because a timer expired.
                bool stalled=view.tick-tacticalStarted>Seconds(pace.waveDuration)&&opposition.Length==0
                    &&active.All(e=>e.order==CommandKind.Stop||e.order==CommandKind.Hold);
                if(losing||broken||stalled)
                {
                    foreach(var unit in active)
                    {
                        var safe=SafeRally(own,bases,enemies,unit.position);Return(unit,safe,view.tick);unavailable.Add(unit.id);
                        Issue(commands,view,new[]{unit},CommandKind.Move,safe);
                    }
                    tacticalActive=false;tacticalNextAttack=view.tick+Seconds(pace.waveRest);active=Array.Empty<VisibleEntity>();
                }
            }

            bool knownBase=view.entities.Any(e=>e.owner!=player&&e.health>0&&Type(e).building);
            // A scout uses ordinary movement, not an attack wave. Its destination
            // comes from public map layout and unexplored cells, never hidden units.
            if(view.tick>=Seconds(pace.scoutStart)&&(!knownBase||view.tick<Seconds(pace.firstWave)))
            {
                var scout=army.FirstOrDefault(e=>e.id==tacticalScout&&!unavailable.Contains(e.id)&&!tacticalAttackers.Contains(e.id))
                    ??army.Where(e=>Type(e).role=="scout"&&!unavailable.Contains(e.id)&&!tacticalAttackers.Contains(e.id)).OrderBy(e=>e.id).FirstOrDefault();
                if(scout!=null)
                {
                    bool fresh=scout.id!=tacticalScout;tacticalScout=scout.id;unavailable.Add(scout.id);
                    var danger=enemies.Where(ArmedThreat).OrderBy(e=>Center(e).Distance(scout.position)).FirstOrDefault();
                    if(danger!=null&&Center(danger).Distance(scout.position)<Math.Max(5,Type(danger).range+2))
                    {
                        var away=Point.Towards(scout.position,home,6);Issue(commands,view,new[]{scout},CommandKind.Move,away);
                    }
                    else
                    {
                        if(fresh||scout.position.Distance(tacticalScoutGoal)<3||scout.order==CommandKind.Stop)
                            tacticalScoutGoal=ExploreDestination(view,scout.position,home,true);
                        Issue(commands,view,new[]{scout},CommandKind.Move,tacticalScoutGoal);
                    }
                }
            }
            else tacticalScout=0;

            var available=army.Where(e=>!unavailable.Contains(e.id)&&!tacticalAttackers.Contains(e.id)&&e.health>=Type(e).health*.5f&&e.order!=CommandKind.Cast).ToArray();
            if(!tacticalActive&&view.tick>=Math.Max(Seconds(pace.firstWave),tacticalNextAttack)&&available.Length>=pace.waveSize)
            {
                int count=Math.Min(pace.waveLimit,Math.Max(pace.waveSize,(int)Math.Ceiling(available.Length*pace.attackShare)));
                active=BalancedForce(available,count);
                foreach(var unit in active)tacticalAttackers.Add(unit.id);
                tacticalActive=true;tacticalStarted=view.tick;tacticalPeak=active.Length;
                tacticalDestination=AttackDestination(view,ArmyCenter(active),home,out tacticalTarget);
                Issue(commands,view,active,CommandKind.AttackMove,tacticalDestination);
            }
            else if(tacticalActive)
            {
                active=army.Where(e=>tacticalAttackers.Contains(e.id)&&!unavailable.Contains(e.id)).ToArray();
                bool gone=tacticalTarget>0&&!view.entities.Any(e=>e.id==tacticalTarget&&e.health>0);
                bool discovered=tacticalTarget==0&&knownBase;
                bool arrived=active.Length>0&&active.All(e=>e.position.Distance(tacticalDestination)<9)
                    &&!enemies.Any(e=>Center(e).Distance(tacticalDestination)<12);
                if(active.Length>0&&(gone||discovered||arrived))
                {
                    tacticalDestination=AttackDestination(view,ArmyCenter(active),home,out tacticalTarget);
                    tacticalStarted=view.tick;
                }
                Issue(commands,view,active,CommandKind.AttackMove,tacticalDestination);
                // Reinforce in useful groups. Keep the pair waiting at home until it
                // can travel with a mixed detachment, instead of feeding single units.
                int desired=Math.Min(pace.waveLimit,Math.Max(pace.waveSize,(int)Math.Ceiling((active.Length+available.Length)*pace.attackShare)));
                int room=desired-active.Length,minBatch=Math.Max(3,pace.waveSize/2);
                if(room>=minBatch&&available.Length>=minBatch)
                {
                    var reinforcements=BalancedForce(available,Math.Min(room,available.Length));
                    foreach(var unit in reinforcements)tacticalAttackers.Add(unit.id);
                    tacticalPeak=Math.Max(tacticalPeak,tacticalAttackers.Count);
                    Issue(commands,view,reinforcements,CommandKind.AttackMove,tacticalDestination);
                }
            }

            var staging=army.Where(e=>!unavailable.Contains(e.id)&&!tacticalAttackers.Contains(e.id)&&e.id!=tacticalScout&&e.order!=CommandKind.Cast).ToArray();
            foreach(var group in staging.GroupBy(e=>bases.OrderBy(b=>e.position.Distance(Center(b))).ThenBy(b=>b.id).First().id))
            {
                var localRally=TacticalRally(own,bases.First(b=>b.id==group.Key),true);var moving=new List<VisibleEntity>();
                foreach(var unit in group)
                {
                    // A defender can still be fighting after its enemy crosses the
                    // base alarm radius. Assembly must not cancel that engagement.
                    bool engaging=unit.order==CommandKind.Stop||unit.order==CommandKind.Attack||unit.order==CommandKind.AttackMove;
                    if(engaging&&enemies.Any(e=>Center(e).Distance(unit.position)<=Type(unit).vision+(Type(e).building?Type(e).footprint*.4f:0)))continue;
                    bool arrived=unit.position.Distance(localRally)<5;
                    // Idle reserves must yield to scouts and new production in
                    // narrow base exits. Hold would make these bodies immovable.
                    if(arrived){if(unit.order!=CommandKind.Stop)commands.Add(Command(CommandKind.Stop,unit.id));}
                    else moving.Add(unit);
                }
                Issue(commands,view,moving.ToArray(),CommandKind.Move,localRally);
            }
            // Casts follow movement decisions and each caster is activated at most
            // once per reaction. Predicted damage/healing prevents redundant volleys.
            commands.AddRange(TacticalAbilities(view,own,enemies));
            return commands;
        }

        private void Return(VisibleEntity unit,Point point,int tick)
        {tacticalReturning[unit.id]=(point,tick);tacticalAttackers.Remove(unit.id);tacticalOrders.Remove(unit.id);}
        private bool ArmedThreat(VisibleEntity e)=>Type(e).damage>0&&(Type(e).role!="worker"||e.attackTick>=tacticalTick-Seconds(2));
        private float CombatPower(VisibleEntity e)
        {
            var type=Type(e);float health=Math.Max(0,Math.Min(1,e.health/type.health));
            float dps=type.damage*Simulation.TickRate/Math.Max(1,type.attackPeriod);
            if(type.abilities.Length>0&&e.owner==player&&e.energy>=40)dps+=8;
            return Math.Max(2,dps)*(float)Math.Sqrt(type.health/100)*(.35f+.65f*health);
        }
        private static Point ArmyCenter(VisibleEntity[] units)=>units.Length==0?default:new Point(units.Average(e=>e.position.x),units.Average(e=>e.position.y));
        private VisibleEntity[] BalancedForce(VisibleEntity[] units,int count)
        {
            var result=new List<VisibleEntity>();
            var queues=new[]{"melee","ranged","siege","caster","scout"}.Select(role=>new Queue<VisibleEntity>(units.Where(e=>Type(e).role==role).OrderByDescending(e=>e.health/Type(e).health).ThenBy(e=>e.id))).ToArray();
            while(result.Count<count&&queues.Any(q=>q.Count>0))foreach(var queue in queues)if(queue.Count>0&&result.Count<count)result.Add(queue.Dequeue());
            return result.ToArray();
        }
        private void Issue(List<GameCommand> commands,PlayerView view,VisibleEntity[] units,CommandKind kind,Point point)
        {
            var selected=new List<VisibleEntity>();
            foreach(var unit in units)
            {
                if(unit.order==CommandKind.Cast)continue;
                bool same=tacticalOrders.TryGetValue(unit.id,out var prior)&&prior.kind==kind&&prior.point.Distance(point)<3;
                bool active=unit.order==kind;
                bool fighting=kind==CommandKind.AttackMove&&unit.attackTick>=view.tick-Seconds(2)&&unit.attackTick>=0;
                if((same&&active)||fighting)continue;
                if(kind==CommandKind.Move&&unit.order==CommandKind.Move&&unit.destination.Distance(point)<3)continue;
                selected.Add(unit);tacticalOrders[unit.id]=(kind,point);
            }
            if(selected.Count>0)commands.Add(Group(kind,selected.ToArray(),point));
        }
        private Point TacticalRally(VisibleEntity[] own,VisibleEntity headquarters,bool assembling=false)
        {
            var home=Center(headquarters);var spawn=terrain.Definition.spawns.First(s=>s.player!=player);
            var opponent=new Point(spawn.x+2,spawn.y+2);
            var desired=Point.Towards(home,assembling?opponent:new Point(2*home.x-opponent.x,2*home.y-opponent.y),6);
            var candidates=new List<Point>();
            for(int y=-9;y<=9;y++)for(int x=-9;x<=9;x++)
            {
                var p=new Point((int)home.x+x+.5f,(int)home.y+y+.5f);float d=p.Distance(home);
                if(d<5||d>10||!terrain.CanStand(p,.49f)||own.Any(e=>Type(e).building&&p.x>e.position.x-1&&p.x<e.position.x+Type(e).footprint+1&&p.y>e.position.y-1&&p.y<e.position.y+Type(e).footprint+1)
                    ||terrain.Definition.resources.Any(r=>p.Distance(new Point(r.x+.5f,r.y+.5f))<3))continue;
                candidates.Add(p);
            }
            return candidates.Count>0?candidates.OrderBy(p=>p.Distance(desired)).First():home;
        }
        private Point SafeRally(VisibleEntity[] own,VisibleEntity[] bases,VisibleEntity[] enemies,Point origin)
        {
            var headquarters=bases.OrderBy(b=>origin.Distance(Center(b))+enemies.Where(e=>ArmedThreat(e)&&Center(e).Distance(Center(b))<14).Sum(CombatPower)*5).ThenBy(b=>b.id).First();
            return TacticalRally(own,headquarters);
        }
        private Point AttackDestination(PlayerView view,Point origin,Point home,out int target)
        {
            var building=view.entities.Where(e=>e.owner!=player&&e.health>0&&Type(e).building)
                .OrderBy(e=>origin.Distance(Center(e))*(Type(e).role=="hq"?.88f:1)).ThenBy(e=>e.id).FirstOrDefault();
            target=building?.id??0;
            return building!=null?Center(building):ExploreDestination(view,origin,home,false);
        }
        private Point ExploreDestination(PlayerView view,Point origin,Point home,bool scout)
        {
            var spawn=terrain.Definition.spawns.First(s=>s.player!=player);var opponent=new Point(spawn.x+2,spawn.y+2);
            var entry=scout?Point.Towards(opponent,home,9):opponent;
            int cell=terrain.Cell(entry);
            if(cell>=0&&(view.visibility[cell]==0||!scout&&view.visibility[terrain.Cell(opponent)]==0))return entry;
            var sites=terrain.Definition.resources.Where(r=>r.kind=="mineral").Select(r=>new Point(r.x+.5f,r.y+.5f))
                .Where(p=>terrain.Cell(p)>=0&&view.visibility[terrain.Cell(p)]==0).OrderBy(p=>origin.Distance(p)).ToArray();
            if(sites.Length>0)return sites[0];
            var candidates=new List<Point>();
            for(int y=6;y<terrain.Height-4;y+=10)for(int x=6;x<terrain.Width-4;x+=10)
            {var p=new Point(x+.5f,y+.5f);if(terrain.CanStand(p,.49f)&&view.visibility[terrain.Cell(p)]==0)candidates.Add(p);}
            if(candidates.Count>0)return candidates.OrderBy(p=>origin.Distance(p)).First();
            tacticalPatrol=(tacticalPatrol+1)%4;
            return new Point(tacticalPatrol%2==0?terrain.Width*.3f:terrain.Width*.7f,tacticalPatrol/2==0?terrain.Height*.3f:terrain.Height*.7f);
        }

        private IEnumerable<GameCommand> TacticalAbilities(PlayerView view,VisibleEntity[] own,VisibleEntity[] enemies)
        {
            var result=new List<GameCommand>();var health=own.Concat(enemies).ToDictionary(e=>e.id,e=>e.health);
            int budget=Difficulty==AiDifficulty.Easy?1:Difficulty==AiDifficulty.Normal?2:4;
            foreach(var caster in own.Where(e=>e.complete&&e.health>0&&e.order!=CommandKind.Cast&&Type(e).abilities.Length>0).OrderBy(e=>e.id))
            {
                AbilityDefinition best=null;Point destination=default;float bestScore=0;
                foreach(string id in Type(caster).abilities)
                {
                    var ability=catalog.Ability(id);
                    if(ability==null||caster.energy<ability.energyCost||caster.abilityCooldowns.Any(c=>c.ability==id&&c.readyTick>view.tick))continue;
                    bool heal=ability.effect=="heal";
                    var affected=heal?own.Where(e=>e.complete&&health[e.id]>0&&!Type(e).building&&health[e.id]<Type(e).health).ToArray():enemies.Where(e=>health[e.id]>0).ToArray();
                    foreach(var target in affected)
                    {
                        var point=Center(target);int cell=terrain.Cell(point);
                        if(cell<0||view.visibility[cell]!=2||caster.position.Distance(point)>ability.range||!terrain.HasLineOfSight(caster.position,point))continue;
                        float amount=affected.Where(e=>Center(e).Distance(point)<=ability.radius).Sum(e=>Math.Min(ability.amount,heal?Type(e).health-health[e.id]:health[e.id]));
                        if(amount<ability.amount*(heal?.65f:.85f))continue;
                        float score=amount*(heal&&health[target.id]<Type(target).health*.35f?1.6f:1);
                        if(score>bestScore){bestScore=score;best=ability;destination=point;}
                    }
                }
                if(best==null)continue;
                result.Add(Command(CommandKind.Cast,caster.id,definition:best.id,position:destination));
                foreach(var target in (best.effect=="heal"?own:enemies).Where(e=>Center(e).Distance(destination)<=best.radius))
                    health[target.id]=best.effect=="heal"?Math.Min(Type(target).health,health[target.id]+best.amount):Math.Max(0,health[target.id]-best.amount);
                if(result.Count>=budget)break;
            }
            return result;
        }
    }
}
