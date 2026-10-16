using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Simulation
    {
        private CommandResult ConfigureRallyPoint(GameCommand command, List<EntityState> selected)
        {
            if (selected.Any(e => !e.complete || e.health <= 0 || !Catalog.CanProduceUnits(e.definition)))
                return Reject("Select completed buildings that produce units.");
            if (command.kind == CommandKind.ClearRallyPoint)
            {
                foreach (var producer in selected) { producer.hasRallyPoint = false; producer.rallyPoint = default; }
                return Accept();
            }
            if (!Finite(command.position) || Map.Cell(command.position) < 0) return Reject("Rally point outside the map.");
            // Like movement destinations, rally markers use cell centers. Validate every
            // producer before changing any of them, and allow temporarily occupied exits.
            Point point = Map.Center(Map.Cell(command.position));
            var knownMap = RallyPlanningMap(command.player);
            var knownPaths = new Pathfinder(knownMap);
            foreach (var producer in selected)
            {
                var type = Type(producer);
                float radius = Catalog.Definition.archetypes.Where(a => !a.building && a.faction == type.faction && a.producer == type.role).Max(a => a.collisionRadius);
                if (!knownMap.CanStand(point, radius)) return Reject("Rally point needs open terrain.");
                bool reachable = RallyExitCells(producer, radius, knownMap).Any(exit => knownPaths.Find(exit, point, .1f, null, radius) != null);
                if (!reachable) return Reject("No path from the selected building to that rally point.");
            }
            foreach (var producer in selected) { producer.hasRallyPoint = true; producer.rallyPoint = point; }
            return Accept();
        }

        private GridMap RallyPlanningMap(int player)
        {
            // Acceptance of a command must not become a probe for buildings behind fog.
            // Keep public terrain and unknown/static occupancy, then replace hidden enemy
            // footprints with the last observed building state available to this player.
            var known = new GridMap(Map.Definition);
            Array.Copy(Map.Walkable, known.Walkable, Map.Count);
            Array.Copy(Map.Buildable, known.Buildable, Map.Count);
            Array.Copy(Map.Ramp, known.Ramp, Map.Count);
            Array.Copy(Map.Elevation, known.Elevation, Map.Count);
            Array.Copy(Map.SurfaceLevel, known.SurfaceLevel, Map.Count);
            Array.Copy(Map.Occupant, known.Occupant, Map.Count);
            foreach (var entity in Entities.Values)
            {
                if (entity.owner == player || !Type(entity).building || Visible(player, Center(entity))) continue;
                int size = Type(entity).footprint;
                for (int y = (int)entity.position.y; y < entity.position.y + size; y++)
                    for (int x = (int)entity.position.x; x < entity.position.x + size; x++)
                    {
                        int cell = known.Index(x, y);
                        if (known.Occupant[cell] == entity.id) known.Occupant[cell] = 0;
                    }
            }
            foreach (var memory in memories[player].Values)
            {
                int size = Catalog[memory.definition].footprint;
                var center = new Point(memory.position.x + size * .5f, memory.position.y + size * .5f);
                if (!Visible(player, center)) known.Occupy(memory.position, size, memory.id);
            }
            return known;
        }

        private IEnumerable<Point> RallyExitCells(EntityState producer, float radius, GridMap grid = null)
        {
            grid = grid ?? Map;
            int size = Type(producer).footprint, x0 = (int)producer.position.x, y0 = (int)producer.position.y;
            for (int y = y0 - 1; y <= y0 + size; y++) for (int x = x0 - 1; x <= x0 + size; x++)
            {
                if (x != x0 - 1 && x != x0 + size && y != y0 - 1 && y != y0 + size) continue;
                if (!grid.Inside(x, y)) continue;
                int cell = grid.Index(x, y); Point exit = grid.Center(cell);
                if (grid.Traversable(grid.Cell(Center(producer)), cell) && grid.CanStand(exit, radius)) yield return exit;
            }
        }

        private Point? FreeRallyExit(EntityState producer, float radius)
        {
            Point? fallback = null;
            bool targetOpen = Map.CanStand(producer.rallyPoint, radius);
            foreach (var exit in RallyExitCells(producer, radius))
            {
                if (!ClearForSpawn(exit, radius)) continue;
                if (!fallback.HasValue) fallback = exit;
                // Prefer an exit connected to the rally, rather than spawning into an
                // enclosed pocket on the other side of the building.
                if (targetOpen && paths.Find(exit, producer.rallyPoint, .1f, null, radius) != null) return exit;
            }
            return fallback;
        }

        private void RallyProducedUnit(EntityState producer, EntityState unit)
        {
            float radius = Type(unit).collisionRadius;
            bool reachable = Map.CanStand(producer.rallyPoint, radius)
                && paths.Find(unit.position, producer.rallyPoint, .1f, null, radius) != null;
            if (reachable)
            {
                // Reuse normal movement planning so consecutive spawns reserve separate
                // destinations and obey the existing inertia, collision and traffic rules.
                var result = ExecuteCommand(new GameCommand { player = producer.owner, kind = CommandKind.Move,
                    entities = new[] { unit.id }, position = producer.rallyPoint }, new List<EntityState> { unit }, false);
                if (result.accepted) return;
            }
            // Do not stall a finished production item or reinterpret a worker rally as
            // gathering. The unit remains at its safe exit and can receive new orders.
            // Only describe a blocked route when the owner has evidence of the blockage;
            // otherwise spawning must not remotely announce hidden enemy construction.
            var knownMap = RallyPlanningMap(producer.owner);
            bool knownBlocked = !knownMap.CanStand(producer.rallyPoint, radius)
                || new Pathfinder(knownMap).Find(unit.position, producer.rallyPoint, .1f, null, radius) == null;
            if (knownBlocked)
                Notice?.Invoke(producer.owner, "Rally point blocked. " + Type(unit).name + " is waiting outside " + Type(producer).name + ".");
        }
    }
}
