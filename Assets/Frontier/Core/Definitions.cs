using System;
using System.Collections.Generic;

namespace Frontier.Core
{
    [Serializable]
    public struct Point
    {
        public float x, y;
        public Point(float x, float y) { this.x = x; this.y = y; }
        public float Distance(Point other) { float a = x - other.x, b = y - other.y; return (float)Math.Sqrt(a * a + b * b); }
        public static Point Towards(Point from, Point to, float distance)
        {
            float length = from.Distance(to);
            if (length <= distance || length < .0001f) return to;
            return new Point(from.x + (to.x - from.x) * distance / length, from.y + (to.y - from.y) * distance / length);
        }
    }

    [Serializable] public sealed class TerrainRegion
    {
        public int x, y, width = 1, height = 1, elevation;
        public bool walkable = true, buildable = true, ramp;
        public string rampAxis = "x";
        public int rampDirection = 1;
    }
    [Serializable] public sealed class SpawnDefinition { public int player; public int x, y; }
    [Serializable] public sealed class ResourceDefinition
    {
        public string id, kind = "mineral";
        public int x, y, amount = 3000;
    }
    [Serializable] public sealed class DecorationDefinition { public string id = "rock"; public int x, y; public float rotation; }
    [Serializable]
    public sealed class MapDefinition
    {
        public int schemaVersion = 1;
        public string id, title;
        public int width = 128, height = 128;
        public TerrainRegion[] terrain = Array.Empty<TerrainRegion>();
        public SpawnDefinition[] spawns = Array.Empty<SpawnDefinition>();
        public ResourceDefinition[] resources = Array.Empty<ResourceDefinition>();
        public DecorationDefinition[] decoration = Array.Empty<DecorationDefinition>();
    }
    [Serializable]
    public sealed class Archetype
    {
        public string id, faction, name, role, producer, requires;
        public bool building;
        public int minerals, catalyst, duration, footprint = 1, supply, capacity;
        public float health, speed, damage, range, vision = 9;
        public float collisionRadius = .45f;
        public float collisionMass = 1, acceleration = 16, deceleration = 24;
        public int attackPeriod = 20;
    }
    [Serializable] public sealed class FactionDefinition
    {
        public string id, name;
        public bool grows, regenerates;
    }
    [Serializable] public sealed class CatalogDefinition
    {
        public int schemaVersion = 1;
        public FactionDefinition[] factions = Array.Empty<FactionDefinition>();
        public Archetype[] archetypes = Array.Empty<Archetype>();
        public UpgradeDefinition[] upgrades = Array.Empty<UpgradeDefinition>();
    }
    [Serializable] public sealed class UpgradeDefinition
    {
        public string id, name;
        public int minerals = 150, catalyst = 75, duration = 600;
    }
    public sealed class Catalog
    {
        public readonly CatalogDefinition Definition;
        private readonly Dictionary<string, Archetype> types = new Dictionary<string, Archetype>();
        private readonly Dictionary<string, FactionDefinition> factions = new Dictionary<string, FactionDefinition>();
        public Catalog(CatalogDefinition definition)
        {
            if (definition == null || definition.schemaVersion != 1) throw new ArgumentException("Incompatible catalog version.");
            if (definition.factions == null || definition.archetypes == null || definition.upgrades == null) throw new ArgumentException("Incomplete catalog.");
            Definition = definition;
            foreach (var faction in definition.factions)
            {
                if (faction == null || string.IsNullOrWhiteSpace(faction.id) || factions.ContainsKey(faction.id)) throw new ArgumentException("Invalid or duplicate faction.");
                factions.Add(faction.id, faction);
            }
            foreach (var type in definition.archetypes)
            {
                if (type == null || string.IsNullOrWhiteSpace(type.id) || types.ContainsKey(type.id) || type.faction == null || !factions.ContainsKey(type.faction)
                    || !Positive(type.health) || !Nonnegative(type.speed) || !Nonnegative(type.damage) || !Nonnegative(type.range) || !Positive(type.vision)
                    || (!type.building && (!Positive(type.collisionRadius) || type.collisionRadius > .49f
                        || !Positive(type.collisionMass) || !Positive(type.acceleration) || !Positive(type.deceleration)))
                    || type.duration < 1 || type.attackPeriod < 1 || type.footprint < 1 || type.footprint > 8 || type.minerals < 0 || type.catalyst < 0 || type.capacity < 0)
                    throw new ArgumentException("Invalid definition: " + type?.id);
                types.Add(type.id, type);
            }
            foreach (var faction in definition.factions)
            {
                var roles = new HashSet<string>();
                foreach (var type in definition.archetypes) if (type.faction == faction.id)
                    if (string.IsNullOrWhiteSpace(type.role) || !roles.Add(type.role)) throw new ArgumentException("Invalid or duplicate role: " + type.id);
                foreach (string role in new[]{"worker","scout","melee","ranged","siege","hq","extractor","supply","barracks","tech","defense"})
                    if (!roles.Contains(role)) throw new ArgumentException("Missing role " + role + " in " + faction.id);
                if (Role(faction.id,"hq").footprint != 4) throw new ArgumentException("The v1 starting base must occupy 4 x 4 cells.");
                foreach (var type in definition.archetypes) if (type.faction == faction.id)
                {
                    if (!type.building && (string.IsNullOrEmpty(type.producer) || !roles.Contains(type.producer) || !Role(faction.id,type.producer).building)) throw new ArgumentException("Invalid producer: " + type.id);
                    if (!string.IsNullOrEmpty(type.requires) && (!roles.Contains(type.requires) || !Role(faction.id,type.requires).building)) throw new ArgumentException("Invalid prerequisite: " + type.id);
                }
            }
            var upgrades = new HashSet<string>();
            foreach (var upgrade in definition.upgrades)
                if (upgrade == null || (upgrade.id != "attack" && upgrade.id != "armor") || !upgrades.Add(upgrade.id) || upgrade.minerals < 0 || upgrade.catalyst < 0 || upgrade.duration < 1)
                    throw new ArgumentException("Invalid or duplicate upgrade.");
        }
        private static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        private static bool Positive(float value) => Nonnegative(value) && value > 0;
        public Archetype this[string id] => types[id];
        public bool Has(string id) => id != null && types.ContainsKey(id);
        public bool CanProduceUnits(string definition)
        {
            if (!Has(definition) || !types[definition].building) return false;
            var producer = types[definition];
            foreach (var unit in Definition.archetypes)
                if (!unit.building && unit.faction == producer.faction && unit.producer == producer.role) return true;
            return false;
        }
        public FactionDefinition Faction(string id) => factions[id];
        public Archetype Role(string faction, string role)
        {
            foreach (var a in Definition.archetypes) if (a.faction == faction && a.role == role) return a;
            throw new ArgumentException("Unknown role: " + faction + "/" + role);
        }
        public UpgradeDefinition Upgrade(string id)
        {
            foreach (var u in Definition.upgrades) if (u.id == id) return u;
            return null;
        }
    }
    public enum CommandKind { Move, Attack, AttackMove, Gather, Repair, Build, Train, Research, Cancel, Stop, Hold, Surrender, SetRallyPoint, ClearRallyPoint }
    [Serializable]
    public sealed class GameCommand
    {
        public CommandKind kind;
        public int player;
        public int sequence;
        public bool append;
        // A second tap upgrades this earlier action in place instead of adding a waypoint.
        public int replacePendingSequence;
        public int[] entities = Array.Empty<int>();
        public int target;
        // Zero preserves Cancel's legacy behavior (last queued item / unfinished building).
        public int queueOrderId;
        public Point position;
        public string definition;
    }
    public readonly struct CommandResult
    {
        public readonly bool accepted;
        public readonly string message;
        public CommandResult(bool accepted, string message = "") { this.accepted = accepted; this.message = message; }
    }
    [Serializable]
    public sealed class ProductionOrder
    {
        public int id;
        public string definition;
        public bool research;
        public int remaining, total;
    }
    [Serializable]
    public sealed class UnitOrder
    {
        public int sourceSequence, target;
        public CommandKind kind;
        public Point position;
        public string definition;
        [NonSerialized] internal long trafficPriority;
        [NonSerialized] internal float trafficMass;
    }
    [Serializable]
    public sealed class EntityState
    {
        public int id, owner;
        public string definition;
        public Point position;
        public float health;
        public bool complete = true;
        public int remaining, lastDamageTick = -1000, attackReadyTick;
        public CommandKind order = CommandKind.Stop;
        public int target, resourceId, cargo;
        public string cargoKind;
        public Point destination;
        public bool hasRallyPoint;
        public Point rallyPoint;
        public List<ProductionOrder> queue = new List<ProductionOrder>();
        public int activeOrderSequence;
        public List<UnitOrder> pendingOrders = new List<UnitOrder>();
        [NonSerialized] internal List<int> path;
        [NonSerialized] internal int pathIndex, pathVersion = -1, blockedTicks;
        [NonSerialized] internal Point pathGoal, stepOrigin, velocity, yieldGoal;
        [NonSerialized] internal float pathRange;
        [NonSerialized] internal float progressDistance = float.MaxValue;
        [NonSerialized] internal int repathAt, yieldUntil, yieldFor;
        [NonSerialized] internal int yieldSearchAt, yieldDeadline;
        [NonSerialized] internal long trafficPriority, yieldPriority;
        [NonSerialized] internal float trafficMass;
        [NonSerialized] internal Point yieldOrigin, yieldDirection;
        [NonSerialized] internal bool moveGoal, settled, braking;
        [NonSerialized] internal int resumeMoveTick;
        [NonSerialized] internal bool depositing;
    }
    [Serializable] public sealed class PlayerState
    {
        public int id;
        public string faction;
        public int minerals = 400, catalyst, attackLevel, armorLevel;
    }
    [Serializable] public sealed class ResourceState
    {
        public int id, remaining;
        public string definition, kind;
        public Point position;
        // PlayerView only: own assignments (including travel/delivery) and own operating buildings.
        public int assignedWorkers, optimalWorkers, baseId, extractorId;
    }
    [Serializable] public sealed class VisibleEntity
    {
        public int id, owner, target, attackTick = -1, damageTick = -1;
        public string definition;
        public Point position;
        public float health, progress;
        public bool complete, remembered;
        public CommandKind order;
        public ProductionOrder[] queue = Array.Empty<ProductionOrder>();
        public int activeOrderSequence;
        public Point destination;
        public bool hasRallyPoint;
        public Point rallyPoint;
        public UnitOrder[] pendingOrders = Array.Empty<UnitOrder>();
    }
    [Serializable]
    public struct AttackEvent
    {
        public int id, tick, attacker, owner;
        public string definition, targetDefinition;
        // Centers at the instant of firing, retained even when the target dies.
        public Point origin, destination;
    }
    [Serializable]
    public sealed class PlayerView
    {
        public int player, tick, winner = -1, minerals, catalyst, supply, capacity;
        public string faction;
        public VisibleEntity[] entities = Array.Empty<VisibleEntity>();
        public AttackEvent[] attacks = Array.Empty<AttackEvent>();
        public ResourceState[] resources = Array.Empty<ResourceState>();
        public byte[] visibility = Array.Empty<byte>();
        public string[] researched = Array.Empty<string>();
    }
    [Serializable] public sealed class MatchSnapshot
    {
        public int schemaVersion = 1, tick, winner = -1;
        public string mapId;
        public EntityState[] entities;
        public PlayerState[] players;
        public ResourceState[] resources;
    }
    [Serializable] public sealed class BoardPlacement
    {
        public int schemaVersion = 1;
        public string anchorUuid;
        public float width = 1.2f, yaw, zoom = 1, panX, panY;
    }
}
