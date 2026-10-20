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
        { unit.path=null;unit.blockedTicks=0;unit.progressDistance=float.MaxValue;unit.repathAt=0;unit.yieldUntil=0;unit.yieldFor=0;unit.yieldSearchAt=0;unit.trafficPriority=0;unit.trafficMass=0;unit.moveGoal=false;unit.settled=false;unit.braking=false;unit.resumeMoveTick=0; }
        private long TrafficPriority(EntityState unit)=>unit.trafficPriority>0?unit.trafficPriority:(long)unit.id<<32;
        private float TrafficMass(EntityState unit)=>unit.trafficMass>0?unit.trafficMass:Type(unit).collisionMass;
        private static bool HasTravelOrder(EntityState unit)=>unit.clearingSite!=0||unit.order==CommandKind.Move||unit.order==CommandKind.AttackMove
            ||(unit.path!=null&&unit.order!=CommandKind.Stop&&unit.order!=CommandKind.Hold);
        private static Point TrafficDestination(EntityState unit)=>unit.clearingSite!=0?unit.clearanceGoal:
            unit.order==CommandKind.Move||unit.order==CommandKind.AttackMove?unit.destination:unit.pathGoal;
        private bool HasTrafficPriority(EntityState first,EntityState second)
        {
            if ((first.clearingSite != 0) != (second.clearingSite != 0)) return first.clearingSite != 0;
            long a=TrafficPriority(first),b=TrafficPriority(second);
            if(first.yieldUntil>Tick&&first.yieldPriority==b)return false;
            if(second.yieldUntil>Tick&&second.yieldPriority==a)return true;
            if(first.settled!=second.settled)return !first.settled;
            // A group yields as one flow, even when its members' entity IDs are interleaved.
            if(a!=b)return Math.Abs(TrafficMass(first)-TrafficMass(second))>.001f
                ?TrafficMass(first)>TrafficMass(second):a<b;
            if(first.yieldUntil>Tick&&second.yieldUntil<=Tick)return true;
            if(second.yieldUntil>Tick&&first.yieldUntil<=Tick)return false;
            if((first.order==CommandKind.Move||first.order==CommandKind.AttackMove)
                &&(second.order==CommandKind.Move||second.order==CommandKind.AttackMove))return false;
            return first.id<second.id;
        }
        private void SetPath(EntityState unit,List<int> path,Point goal,float range)
        {
            unit.progressDistance=float.MaxValue;
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
        private bool Travel(EntityState unit,Point destination,float range,bool stopOnFailure=true)
        {
            bool yielding=Tick<unit.yieldUntil;
            if(yielding&&!mobiles.Any(other=>other.id!=unit.id&&other.owner==unit.owner&&other.health>0
                &&TrafficPriority(other)==unit.yieldPriority&&other.yieldUntil<=Tick
                &&HasTravelOrder(other)))
            {unit.yieldUntil=0;unit.path=null;Brake(unit);return false;}
            if(yielding)
            {
                destination=unit.yieldGoal;range=.08f;
                if(Tick<unit.yieldDeadline)unit.yieldUntil=Math.Max(unit.yieldUntil,Tick+20);
            }
            float distance=unit.position.Distance(destination);
            if(distance<=range)
            {
                // A very short replacement MOVE must brake too, not erase momentum
                // simply because its new goal is already inside the arrival radius.
                if(unit.velocity.Distance(default)>Type(unit).deceleration/TickRate+.001f)
                {Brake(unit);return false;}
                unit.path=null;unit.blockedTicks=0;
                if(yielding)
                {
                    bool passing=mobiles.Any(other=>other.id!=unit.id&&other.owner==unit.owner&&TrafficPriority(other)==unit.yieldPriority&&other.health>0&&other.yieldUntil<=Tick
                        &&HasTravelOrder(other)
                        &&(TrafficDestination(other).x-unit.yieldOrigin.x)*unit.yieldDirection.x+(TrafficDestination(other).y-unit.yieldOrigin.y)*unit.yieldDirection.y>0
                        &&(other.position.x-unit.position.x)*unit.yieldDirection.x+(other.position.y-unit.position.y)*unit.yieldDirection.y<3);
                    if(!passing||Tick>=unit.yieldDeadline)unit.yieldUntil=0;
                    else unit.yieldUntil=Math.Max(unit.yieldUntil,Tick+20);
                }
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
                    if(!yielding && stopOnFailure){Notice?.Invoke(unit.owner,"Destination unreachable.");unit.order=CommandKind.Stop;}
                    return false;
                }
                SetPath(unit,route,destination,range);
            }
            while(unit.pathIndex<unit.path.Count&&unit.position.Distance(Map.Center(unit.path[unit.pathIndex]))<.08f)unit.pathIndex++;
            // Skip obsolete cell centres after steering/pushes when the body has a clear
            // route ahead. Respect traffic as well as terrain so shortcuts keep useful detours.
            if(unit.pathIndex<unit.path.Count&&ClearMovementSegment(unit,destination)
                &&Map.CanTraverse(unit.position,destination,Type(unit).collisionRadius))unit.pathIndex=unit.path.Count;
            for(int look=Math.Min(unit.path.Count-1,unit.pathIndex+12);look>unit.pathIndex;look--)
            {
                var ahead=Map.Center(unit.path[look]);
                if(!ClearMovementSegment(unit,ahead)||!Map.CanTraverse(unit.position,ahead,Type(unit).collisionRadius))continue;
                unit.pathIndex=look;break;
            }
            Point waypoint=unit.pathIndex<unit.path.Count?Map.Center(unit.path[unit.pathIndex]):destination;
            // Empty routes (same cell) still need the final fractional movement.
            float step=Math.Max(0,Type(unit).speed/TickRate-unit.stepOrigin.Distance(unit.position));
            if(step<.0001f)return false;
            var type=Type(unit);
            float remainingDistance=unit.position.Distance(waypoint)+Math.Max(0,unit.path.Count-unit.pathIndex-1);
            if(unit.pathIndex<unit.path.Count)remainingDistance+=Map.Center(unit.path[unit.path.Count-1]).Distance(destination);
            float speed=Math.Min(type.speed,(float)Math.Sqrt(2*type.deceleration*Math.Max(0,remainingDistance-range)));
            float waypointDistance=Math.Max(.0001f,unit.position.Distance(waypoint));
            var desired=new Point((waypoint.x-unit.position.x)*speed/waypointDistance,(waypoint.y-unit.position.y)*speed/waypointDistance);
            var preferred=AcceleratedStep(unit,desired,step);
            if(!Map.CanTraverse(unit.position,preferred,Type(unit).collisionRadius))
            {
                // Contact cancels only the blocked component of the accelerated displacement.
                var slideX=new Point(preferred.x,unit.position.y);
                var slideY=new Point(unit.position.x,preferred.y);
                if(slideY.Distance(waypoint)<slideX.Distance(waypoint))
                {var swap=slideX;slideX=slideY;slideY=swap;}
                bool slid=slideX.Distance(unit.position)>.0001f&&TryStep(unit,slideX,false);
                if(!slid)slid=slideY.Distance(unit.position)>.0001f&&TryStep(unit,slideY,false);
                if(slid)
                {
                    float slidingRemaining=unit.position.Distance(waypoint)+Math.Max(0,unit.path.Count-unit.pathIndex-1);
                    if(slidingRemaining<unit.progressDistance-.2f){unit.progressDistance=slidingRemaining;unit.blockedTicks=0;}
                    else unit.blockedTicks++;
                    return !yielding&&unit.position.Distance(destination)<=range
                        &&unit.stepOrigin.Distance(unit.position)*TickRate<=Type(unit).deceleration/TickRate+.001f;
                }
            }
            Point best=unit.position;float bestScore=float.MaxValue;
            float dx=desired.x/TickRate,dy=desired.y/TickRate;
            localTraffic.Clear();
            foreach(var other in mobiles)
                if(other.id!=unit.id&&other.health>0&&unit.position.Distance(other.position)<3)localTraffic.Add(other);
            foreach(var other in localTraffic)
                if(other.owner==unit.owner&&HasTrafficPriority(unit,other)&&unit.position.Distance(other.position)<1.8f
                    &&(other.order==CommandKind.Move||other.order==CommandKind.AttackMove)
                    &&dx*(other.destination.x-other.position.x)+dy*(other.destination.y-other.position.y)<-.1f)
                    RequestYield(other,unit,waypoint);
            foreach(float angle in steeringAngles)
            {
                float cos=(float)Math.Cos(angle),sin=(float)Math.Sin(angle);
                var candidate=AcceleratedStep(unit,new Point(desired.x*cos-desired.y*sin,desired.x*sin+desired.y*cos),step);
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
            // On an open approach, steer toward the actual target. Choosing a rotated
            // candidate merely because it brakes a little sooner creates arrival spirals.
            bool moved=(localTraffic.Count==0||(unit.pathIndex==unit.path.Count&&ClearMovementSegment(unit,destination)))
                &&TryStep(unit,preferred,false);
            if(!moved)moved=best.Distance(waypoint)>=unit.position.Distance(waypoint)-.005f&&TryStep(unit,preferred,true);
            if(!moved)moved=bestScore<float.MaxValue&&TryStep(unit,best,false);
            if(!moved) moved=TryStep(unit,preferred,true);
            // Advancing around a wall can increase straight-line distance to the destination.
            // Count the remaining route instead, so legitimate detours do not trigger yielding.
            float remaining=unit.position.Distance(waypoint)+Math.Max(0,unit.path.Count-unit.pathIndex-1);
            if(moved&&remaining<unit.progressDistance-.2f){unit.progressDistance=remaining;unit.blockedTicks=0;}else unit.blockedTicks++;
            if(unit.blockedTicks>=12)
            {
                foreach(var other in mobiles)
                    if(other.owner==unit.owner&&HasTrafficPriority(unit,other)&&other.position.Distance(unit.position)<2)
                        RequestYield(other,unit,waypoint);
            }
            return !yielding&&unit.position.Distance(destination)<=range
                &&unit.stepOrigin.Distance(unit.position)*TickRate<=Type(unit).deceleration/TickRate+.001f;
        }
        private Point AcceleratedStep(EntityState unit,Point desired,float budget)
        {
            var type=Type(unit);
            bool braking=desired.Distance(default)<unit.velocity.Distance(default)
                ||desired.x*unit.velocity.x+desired.y*unit.velocity.y<0;
            var velocity=Point.Towards(unit.velocity,desired,(braking?type.deceleration:type.acceleration)/TickRate);
            return Point.Towards(unit.position,new Point(unit.position.x+velocity.x/TickRate,unit.position.y+velocity.y/TickRate),budget);
        }
        private void Brake(EntityState unit)
        {
            var next=AcceleratedStep(unit,default,Type(unit).speed/TickRate);
            if(next.Distance(unit.position)<.0001f||!TryStep(unit,next,false))unit.braking=false;
        }
        private bool ClearMovementSegment(EntityState unit,Point end)
        {
            float dx=end.x-unit.position.x,dy=end.y-unit.position.y,lengthSquared=dx*dx+dy*dy;
            if(lengthSquared<.0001f)return true;
            foreach(var other in mobiles)
            {
                if(other.id==unit.id||other.health<=0||(other.owner!=unit.owner&&!Visible(unit.owner,other.position)))continue;
                float t=((other.position.x-unit.position.x)*dx+(other.position.y-unit.position.y)*dy)/lengthSquared;
                if(t<0||t>1)continue;
                float x=unit.position.x+t*dx-other.position.x,y=unit.position.y+t*dy-other.position.y;
                float separation=Type(unit).collisionRadius+Type(other).collisionRadius+.035f;
                if(x*x+y*y<separation*separation)return false;
            }
            return true;
        }
        private bool TryStep(EntityState unit,Point candidate,bool pushAllies)
        {
            displaced.Clear();previousPositions.Clear();pushing.Clear();
            if(!Displace(unit,candidate,unit.owner,pushAllies,0,Type(unit).collisionMass))
            {
                for(int i=displaced.Count-1;i>=0;i--)displaced[i].position=previousPositions[i];
                return false;
            }
            // A pushed unit retains its command, but its old route may no longer start under its feet.
            for(int i=0;i<displaced.Count;i++)
            {
                var other=displaced[i];
                if(other.id==unit.id)continue;
                // Once parked, accept a safe nudge instead of springing back into the passing flow.
                if(other.yieldUntil>Tick&&previousPositions[i].Distance(other.yieldGoal)<=.12f)
                    other.yieldGoal=other.position;
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
        private bool Displace(EntityState unit,Point candidate,int owner,bool pushAllies,int depth,float pushingMass)
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
                if(!pushAllies||other.owner!=owner||other.order==CommandKind.Hold||pushing.Contains(other.id)
                    ||(pushingMass+.001f<Type(other).collisionMass&&other.yieldUntil<=Tick)
                    ||(unit.yieldUntil>Tick&&TrafficPriority(other)==unit.yieldPriority)
                    ||(other.yieldUntil>Tick&&unit.yieldUntil<=Tick&&TrafficPriority(other)==TrafficPriority(unit)
                        &&other.yieldPriority!=TrafficPriority(unit)))return false;
                float dx=distance>.0001f?(other.position.x-candidate.x)/distance:1;
                float dy=distance>.0001f?(other.position.y-candidate.y)/distance:0;
                var pushed=new Point(other.position.x+dx*(minimum-distance+.001f),other.position.y+dy*(minimum-distance+.001f));
                if(!Displace(other,pushed,owner,true,depth+1,pushingMass))return false;
            }
            pushing.Remove(unit.id);return true;
        }
        private void RequestYield(EntityState blocker,EntityState priority,Point waypoint)
        {
            if(blocker.clearingSite!=0||(blocker.order==CommandKind.Hold&&priority.clearingSite==0)||blocker.yieldUntil>Tick||Tick<blocker.yieldSearchAt)return;
            var direction=Point.Towards(default,new Point(waypoint.x-priority.position.x,waypoint.y-priority.position.y),1);
            float along=(blocker.position.x-priority.position.x)*direction.x+(blocker.position.y-priority.position.y)*direction.y;
            if(along<0)return;
            blocker.yieldSearchAt=Tick+20+blocker.id%7;
            // A packed army may need its outer row to step aside too, even on Hold.
            // Keep this exception scoped to an accepted construction site.
            if (priority.clearingSite != 0 && Entities.TryGetValue(priority.clearingSite, out var site))
            {
                var exit = ConstructionExit(blocker, site.position, Type(site).footprint, new List<(EntityState, Point)>());
                if (exit.HasValue) ClearConstructionSite(blocker, site, exit.Value);
                return;
            }
            float radius=Type(blocker).collisionRadius;
            float clearance=radius+Type(priority).collisionRadius+.05f;
            int distanceLimit=priority.blockedTicks>=12?32:8;
            var route=paths.FindNearby(blocker.position,distanceLimit,c=>
            {
                var goal=Map.Center(c);
                float dx=goal.x-priority.position.x,dy=goal.y-priority.position.y;
                return Math.Abs(dx*direction.y-dy*direction.x)>=1.15f&&ClearForSpawn(goal,radius)
                    &&YieldSpotClearsRoute(priority,goal,clearance+.1f)
                    &&!mobiles.Any(other=>other.id!=blocker.id&&other.yieldUntil>Tick&&other.yieldGoal.Distance(goal)<1);
            },c=>Map.Center(c).Distance(priority.position)<clearance?-1:TrafficCost(blocker,c),radius);
            if(route==null)return;
            var goal=Map.Center(route[route.Count-1]);
            blocker.yieldGoal=goal;
            blocker.yieldUntil=Tick+(int)Math.Ceiling(route.Count/Math.Max(.1f,Type(blocker).speed)*TickRate)+120;
            blocker.yieldDeadline=blocker.yieldUntil+TickRate*60;
            // A retreat propagates the original flow's priority, never a new opposing request
            // from each teammate in the queue. Wait for the entire flow, including gaps in it.
            bool propagating=priority.yieldUntil>Tick&&priority.yieldPriority!=TrafficPriority(priority);
            blocker.yieldFor=propagating?priority.yieldFor:priority.id;
            blocker.yieldPriority=propagating?priority.yieldPriority:TrafficPriority(priority);
            blocker.yieldOrigin=propagating?priority.yieldOrigin:blocker.position;
            blocker.yieldDirection=propagating?priority.yieldDirection:direction;
            if(blocker.moveGoal&&blocker.order==CommandKind.Stop)blocker.order=CommandKind.Move;
            SetPath(blocker,route,goal,.08f);
        }
        private bool YieldSpotClearsRoute(EntityState traveler,Point goal,float clearance)
        {
            Point previous=traveler.position;
            if(traveler.path!=null)
                for(int i=traveler.pathIndex;i<traveler.path.Count;i++)
                {
                    Point next=Map.Center(traveler.path[i]);
                    if(!OutsideSegment(goal,previous,next,clearance))return false;
                    previous=next;
                }
            return OutsideSegment(goal,previous,traveler.pathGoal,clearance);
        }
        private static bool OutsideSegment(Point point,Point from,Point to,float clearance)
        {
            if(point.x<Math.Min(from.x,to.x)-clearance||point.x>Math.Max(from.x,to.x)+clearance
                ||point.y<Math.Min(from.y,to.y)-clearance||point.y>Math.Max(from.y,to.y)+clearance)return true;
            float dx=to.x-from.x,dy=to.y-from.y,lengthSquared=dx*dx+dy*dy;
            float t=lengthSquared<.0001f?0:Math.Max(0,Math.Min(1,((point.x-from.x)*dx+(point.y-from.y)*dy)/lengthSquared));
            float x=point.x-from.x-t*dx,y=point.y-from.y-t*dy;
            return x*x+y*y>=clearance*clearance;
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
