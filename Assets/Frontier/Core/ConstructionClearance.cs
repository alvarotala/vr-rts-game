using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Simulation
    {
        private readonly List<EntityState> constructionSites = new List<EntityState>();

        private IEnumerable<EntityState> ConstructionBlockers(Point origin, int size)
            => Entities.Values.Where(e => e.health > 0 && !Type(e).building
                && OverlapsFootprint(e.position, Type(e).collisionRadius + .08f, origin, size));

        private List<(EntityState unit, Point goal)> PlanConstructionClearance(int owner, Point origin, int size)
        {
            var plan = new List<(EntityState unit, Point goal)>();
            foreach (var unit in ConstructionBlockers(origin, size).Where(e => e.owner == owner).OrderBy(e => e.id))
            {
                var goal = ConstructionExit(unit, origin, size, plan);
                if (!goal.HasValue) return null;
                plan.Add((unit, goal.Value));
            }
            return plan;
        }

        private Point? ConstructionExit(EntityState unit, Point origin, int size, List<(EntityState unit, Point goal)> reserved)
        {
            float radius = Type(unit).collisionRadius;
            var route = paths.FindNearby(unit.position, size + 12, cell =>
            {
                var point = Map.Center(cell);
                return !OverlapsFootprint(point, radius + .3f, origin, size) && ClearForSpawn(point, radius + .1f)
                    && !constructionSites.Any(site => OverlapsFootprint(point, radius + .3f, site.position, Type(site).footprint))
                    && !reserved.Any(item => item.unit.id != unit.id && point.Distance(item.goal) < radius + Type(item.unit).collisionRadius + .2f)
                    && !Entities.Values.Any(other => other.id != unit.id && other.clearingSite != 0
                        && point.Distance(other.clearanceGoal) < radius + Type(other).collisionRadius + .2f);
            }, cell => TrafficCost(unit, cell), radius);
            return route == null ? (Point?)null : Map.Center(route[route.Count - 1]);
        }

        private void ClearConstructionSite(EntityState unit, EntityState site, Point goal)
        {
            // Temporary travel keeps the issued action, targets, cargo and queued orders.
            ResetTravel(unit);
            unit.clearingSite = site.id; unit.clearanceGoal = goal;
        }

        private void UpdateConstructionClearance()
        {
            for (int i = constructionSites.Count - 1; i >= 0; i--)
            {
                var site = constructionSites[i];
                // Keep the reservation until Remove handles the death at the end of this step.
                // A queued build must not reuse terrain that the old site's removal would clear.
                if (site.health <= 0) continue;
                var blockers = ConstructionBlockers(site.position, Type(site).footprint).ToArray();
                if (blockers.Length == 0)
                {
                    Map.Occupy(site.position, Type(site).footprint, site.id);
                    ReleaseConstructionClearance(site);
                    continue;
                }
                // Include allies entering while others leave; retry obstructed exits at a bounded rate.
                if (Tick % 10 != 0) continue;
                foreach (var unit in blockers.Where(e => e.owner == site.owner))
                {
                    if (unit.clearingSite == site.id && unit.blockedTicks < TickRate * 2
                        && Map.CanStand(unit.clearanceGoal, Type(unit).collisionRadius)) continue;
                    var goal = ConstructionExit(unit, site.position, Type(site).footprint, new List<(EntityState, Point)>());
                    if (goal.HasValue) ClearConstructionSite(unit, site, goal.Value);
                }
            }
        }

        private void ReleaseConstructionClearance(EntityState site)
        {
            Map.ReserveConstruction(site.position, Type(site).footprint, 0);
            site.awaitingClearance = false; constructionSites.Remove(site);
            foreach (var unit in Entities.Values.Where(e => e.clearingSite == site.id))
            {
                unit.clearingSite = 0;
                ResetTravel(unit);
            }
        }
    }
}
