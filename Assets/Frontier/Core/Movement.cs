using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Simulation
    {
        private readonly List<EntityState> mobiles = new List<EntityState>();
        private readonly List<EntityState> localTraffic = new List<EntityState>();
        private readonly List<EntityState> displaced = new List<EntityState>();
        private readonly List<Point> previousPositions = new List<Point>();
        private readonly HashSet<int> pushing = new HashSet<int>();
        private static readonly float[] steeringAngles = { 0, -.52f, .52f, -1.05f, 1.05f, -1.57f, 1.57f, -2.1f, 2.1f, 3.14f };

        private void BeginMovementStep(EntityState[] active)
        {
            mobiles.Clear();
            foreach(var unit in active) if(!Type(unit).building)
            { unit.stepOrigin=unit.position; mobiles.Add(unit); }
        }
        private void EndMovementStep()
        {
            foreach(var unit in mobiles)
                unit.velocity=new Point((unit.position.x-unit.stepOrigin.x)*TickRate,(unit.position.y-unit.stepOrigin.y)*TickRate);
        }
        private void ResetTravel(EntityState unit)
        { unit.path=null;unit.blockedTicks=0;unit.progressDistance=float.MaxValue;unit.repathAt=0;unit.yieldUntil=0;unit.yieldFor=0;unit.moveGoal=false;unit.resumeMoveTick=0; }
        private void SetPath(EntityState unit,List<int> path,Point goal,float range)
        {
            if(unit.pathGoal.Distance(goal)>.5f)unit.progressDistance=float.MaxValue;
            unit.path=path;unit.pathIndex=0;unit.pathVersion=Map.Version;unit.pathGoal=goal;unit.pathRange=range;
            unit.repathAt=Tick+20+unit.id%7;
        }
        private IEnumerable<Point> DestinationSlots(Point desired,int radius,HashSet<int> reserved,float bodyRadius)
        {
            int x0=(int)Math.Floor(desired.x),y0=(int)Math.Floor(desired.y);
            var candidates=new List<int>();
            for(int y=Math.Max(0,y0-radius);y<=Math.Min(Map.Height-1,y0+radius);y++)
                for(int x=Math.Max(0,x0-radius);x<=Math.Min(Map.Width-1,x0+radius);x++)
                { int c=Map.Index(x,y);if(!reserved.Contains(c)&&Map.CanStand(Map.Center(c),bodyRadius))candidates.Add(c); }
            foreach(int cell in candidates.OrderBy(c=>Map.Center(c).Distance(desired)).ThenBy(c=>c))yield return Map.Center(cell);
        }
        private int TrafficCost(EntityState traveler,int cell)
        {
            if(unitCells[cell]==0)return 0;
            int cost=0;
            foreach(var other in mobiles)
            {
                if(other.id==traveler.id||other.health<=0||Map.Cell(other.position)!=cell)continue;
                if(other.owner!=traveler.owner&&!Visible(traveler.owner,other.position))continue;
                cost+=other.order==CommandKind.Hold?120:other.velocity.Distance(default)<.15f?45:20;
            }
            return cost;
        }
        private bool Travel(EntityState unit,Point destination,float range)
        {
            bool yielding=Tick<unit.yieldUntil;
            if(yielding){destination=unit.yieldGoal;range=.08f;}
            float distance=unit.position.Distance(destination);
            if(distance<=range)
            {
                unit.path=null;unit.blockedTicks=0;
                if(yielding&&(!Entities.TryGetValue(unit.yieldFor,out var priority)||priority.position.Distance(unit.position)>3))unit.yieldUntil=0;
                return !yielding;
            }
            bool changed=unit.pathGoal.Distance(destination)>.5f||Math.Abs(unit.pathRange-range)>.05f;
            if(unit.path==null||unit.pathVersion!=Map.Version||changed||(unit.blockedTicks>=8&&Tick>=unit.repathAt))
            {
                var route=paths.Find(unit.position,destination,range,c=>TrafficCost(unit,c),Type(unit).collisionRadius);
                if(route==null)
                {
                    unit.repathAt=Tick+20;
                    // Traffic only changes costs: a null route means static terrain actually blocks the order.
                    if(!yielding){Notice?.Invoke(unit.owner,"Destino inaccesible.");unit.order=CommandKind.Stop;}
                    return false;
                }
                SetPath(unit,route,destination,range);
            }
            while(unit.pathIndex<unit.path.Count&&unit.position.Distance(Map.Center(unit.path[unit.pathIndex]))<.08f)unit.pathIndex++;
            Point waypoint=unit.pathIndex<unit.path.Count?Map.Center(unit.path[unit.pathIndex]):destination;
            // Empty routes (same cell) still need the final fractional movement.
            float step=Math.Max(0,Type(unit).speed/TickRate-unit.stepOrigin.Distance(unit.position));
            if(step<.0001f)return false;
            var preferred=Point.Towards(unit.position,waypoint,step);
            Point best=unit.position;float bestScore=float.MaxValue;
            float dx=preferred.x-unit.position.x,dy=preferred.y-unit.position.y;
            localTraffic.Clear();
            foreach(var other in mobiles)
                if(other.id!=unit.id&&other.health>0&&unit.position.Distance(other.position)<3)localTraffic.Add(other);
            foreach(var other in localTraffic)
                if(other.owner==unit.owner&&other.id>unit.id&&unit.position.Distance(other.position)<1.8f
                    &&(other.order==CommandKind.Move||other.order==CommandKind.AttackMove)
                    &&dx*(other.destination.x-other.position.x)+dy*(other.destination.y-other.position.y)<-.1f)
                    RequestYield(other,unit,waypoint);
            foreach(float angle in steeringAngles)
            {
                float cos=(float)Math.Cos(angle),sin=(float)Math.Sin(angle);
                var candidate=new Point(unit.position.x+dx*cos-dy*sin,unit.position.y+dx*sin+dy*cos);
                if(!Map.CanTraverse(unit.position,candidate,Type(unit).collisionRadius))continue;
                float score=candidate.Distance(waypoint)/Math.Max(step,.001f)+Math.Abs(angle)*.08f+(angle>0?.025f:0);
                foreach(var other in localTraffic)
                {
                    float clearance=Type(unit).collisionRadius+Type(other).collisionRadius+.035f;
                    float separation=candidate.Distance(other.position);
                    if(separation<clearance)score+=10+(clearance-separation)*20;
                    // Predict closing traffic, so opposite directions select their own right-hand side early.
                    var relative=new Point(unit.position.x-other.position.x,unit.position.y-other.position.y);
                    var velocity=new Point((candidate.x-unit.position.x)*TickRate-other.velocity.x,(candidate.y-unit.position.y)*TickRate-other.velocity.y);
                    float vv=velocity.x*velocity.x+velocity.y*velocity.y;
                    float time=vv<.001f?0:Math.Max(0,Math.Min(.5f,-(relative.x*velocity.x+relative.y*velocity.y)/vv));
                    float closest=new Point(relative.x+velocity.x*time,relative.y+velocity.y*time).Distance(default);
                    if(closest<clearance)score+=(clearance-closest)*4;
                }
                if(score<bestScore){best=candidate;bestScore=score;}
            }
            bool moved=best.Distance(waypoint)>=unit.position.Distance(waypoint)-.005f&&TryStep(unit,preferred,true);
            if(!moved)moved=bestScore<float.MaxValue&&TryStep(unit,best,false);
            if(!moved) moved=TryStep(unit,preferred,true);
            float remaining=unit.position.Distance(destination);
            if(moved&&remaining<unit.progressDistance-.2f){unit.progressDistance=remaining;unit.blockedTicks=0;}else unit.blockedTicks++;
            if(unit.blockedTicks>=12)
            {
                foreach(var other in mobiles)
                    if(other.owner==unit.owner&&other.id>unit.id&&other.position.Distance(unit.position)<2)
                        RequestYield(other,unit,waypoint);
            }
            return !yielding&&unit.position.Distance(destination)<=range;
        }
        private bool TryStep(EntityState unit,Point candidate,bool pushAllies)
        {
            displaced.Clear();previousPositions.Clear();pushing.Clear();
            if(!Displace(unit,candidate,unit.owner,pushAllies,0))
            {
                for(int i=displaced.Count-1;i>=0;i--)displaced[i].position=previousPositions[i];
                return false;
            }
            // A pushed unit retains its command, but its old route may no longer start under its feet.
            foreach(var other in displaced)if(other.id!=unit.id)
            {
                other.path=null;
                // Let passing traffic clear before reclaiming a completed move destination.
                if(other.moveGoal&&(other.order==CommandKind.Stop||other.resumeMoveTick>Tick))
                {other.order=CommandKind.Move;other.resumeMoveTick=Tick+20;}
            }
            // Update only affected cells; a whole-map clear per unit is costly on Quest.
            for(int i=0;i<displaced.Count;i++)
            { int before=Map.Cell(previousPositions[i]),after=Map.Cell(displaced[i].position);if(before>=0)unitCells[before]=Math.Max(0,unitCells[before]-1);if(after>=0)unitCells[after]++; }
            return true;
        }
        private bool Displace(EntityState unit,Point candidate,int owner,bool pushAllies,int depth)
        {
            if(depth>10||pushing.Contains(unit.id)||unit.stepOrigin.Distance(candidate)>Type(unit).speed/TickRate+.001f
                ||!Map.CanTraverse(unit.position,candidate,Type(unit).collisionRadius))return false;
            pushing.Add(unit.id);
            if(!displaced.Contains(unit)){displaced.Add(unit);previousPositions.Add(unit.position);}
            unit.position=candidate;
            foreach(var other in mobiles)
            {
                if(other.id==unit.id||other.health<=0)continue;
                float minimum=Type(unit).collisionRadius+Type(other).collisionRadius+.005f;
                float distance=candidate.Distance(other.position);
                if(distance>=minimum-.0001f)continue;
                if(!pushAllies||other.owner!=owner||other.order==CommandKind.Hold||pushing.Contains(other.id))return false;
                float dx=distance>.0001f?(other.position.x-candidate.x)/distance:1;
                float dy=distance>.0001f?(other.position.y-candidate.y)/distance:0;
                var pushed=new Point(other.position.x+dx*(minimum-distance+.001f),other.position.y+dy*(minimum-distance+.001f));
                if(!Displace(other,pushed,owner,true,depth+1))return false;
            }
            pushing.Remove(unit.id);return true;
        }
        private void RequestYield(EntityState blocker,EntityState priority,Point waypoint)
        {
            if(blocker.order==CommandKind.Hold||blocker.yieldUntil>Tick)return;
            var direction=Point.Towards(default,new Point(waypoint.x-priority.position.x,waypoint.y-priority.position.y),1);
            var reserved=new HashSet<int>(mobiles.Where(e=>e.id!=blocker.id).Select(e=>Map.Cell(e.position)));
            foreach(var goal in DestinationSlots(blocker.position,6,reserved,Type(blocker).collisionRadius))
            {
                float dx=goal.x-priority.position.x,dy=goal.y-priority.position.y;
                if(Math.Abs(dx*direction.y-dy*direction.x)<1.15f||dx*direction.x+dy*direction.y<0)continue;
                var route=paths.Find(blocker.position,goal,.08f,c=>TrafficCost(blocker,c),Type(blocker).collisionRadius);
                if(route==null||route.Count>12)continue;
                blocker.yieldGoal=goal;blocker.yieldUntil=Tick+80;blocker.yieldFor=priority.id;
                if(blocker.moveGoal&&blocker.order==CommandKind.Stop)blocker.order=CommandKind.Move;
                SetPath(blocker,route,goal,.08f);return;
            }
        }
        private bool ClearForSpawn(Point point,float radius)
        {
            if(!Map.CanStand(point,radius))return false;
            foreach(var other in Entities.Values)
                if(!Type(other).building&&other.health>0&&point.Distance(other.position)<radius+Type(other).collisionRadius+.01f)return false;
            return true;
        }
    }
}
