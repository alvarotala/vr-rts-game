using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Simulation
    {
        public const int TickRate = 20, UnitLimit = 60, BuildingLimit = 20;
        public readonly GridMap Map;
        public readonly Catalog Catalog;
        public readonly PlayerState[] Players = new PlayerState[2];
        public readonly Dictionary<int, EntityState> Entities = new Dictionary<int, EntityState>();
        public readonly List<ResourceState> Resources = new List<ResourceState>();
        public int Tick { get; private set; }
        public int Winner { get; private set; } = -1;
        public bool Paused { get; set; }
        public event Action<int, string> Notice;
        private readonly Pathfinder paths;
        private readonly byte[][] vision;
        private readonly Dictionary<int, VisibleEntity>[] memories;
        private readonly Dictionary<int, int>[] resourceMemories;
        private readonly int[] lastSequences = new int[2];
        private readonly int[] unitCells;
        private int nextId = 1;
        private uint nextMovementOrder;
        // Per-player sequences keep gaps from revealing the other player's hidden production.
        private readonly int[] nextProductionOrderIds = { 1, 1 };

        public Simulation(MapDefinition map, Catalog catalog, string first = "industrial", string second = "biological")
        {
            Map = new GridMap(map); Catalog = catalog; paths = new Pathfinder(Map); unitCells = new int[Map.Count];
            vision = new[] { new byte[Map.Count], new byte[Map.Count] };
            memories = new[] { new Dictionary<int, VisibleEntity>(), new Dictionary<int, VisibleEntity>() };
            resourceMemories = new[] { new Dictionary<int, int>(), new Dictionary<int, int>() };
            Players[0] = new PlayerState { id = 0, faction = first }; Players[1] = new PlayerState { id = 1, faction = second };
            int resourceId = 1;
            foreach (var resource in map.resources) Resources.Add(new ResourceState { id = resourceId++, definition = resource.id, kind = resource.kind, remaining = resource.amount, position = new Point(resource.x + .5f, resource.y + .5f) });
            foreach (var spawn in map.spawns)
            {
                int player = spawn.player;
                var hq = Spawn(player, Catalog.Role(Players[player].faction, "hq").id, new Point(spawn.x, spawn.y));
                for (int i = 0; i < 6; i++)
                {
                    // Authored starting camps are rotated 180 degrees. Mirror the
                    // initial workers too, so neither side starts closer to its minerals.
                    Point? position = FreeExit(hq, reverse: player == 1);
                    if (!position.HasValue) throw new ArgumentException("No space for starting workers.");
                    Spawn(player, Catalog.Role(Players[player].faction, "worker").id, position.Value);
                }
            }
            UpdateVision();
            foreach (var worker in Entities.Values.Where(e => Type(e).role == "worker").OrderBy(e => e.id)) ResumeGather(worker);
        }
        public Archetype Type(EntityState entity) => Catalog[entity.definition];
        public Point Center(EntityState entity)
        {
            var t = Type(entity); return t.building ? new Point(entity.position.x + t.footprint * .5f, entity.position.y + t.footprint * .5f) : entity.position;
        }
        public int Units(int player) => Entities.Values.Count(e => e.owner == player && !Type(e).building);
        public int Buildings(int player) => Entities.Values.Count(e => e.owner == player && Type(e).building);
        public int Reserved(int player) => Entities.Values.Where(e => e.owner == player).Sum(e => e.queue.Count(q => !q.research));
        public int Capacity(int player) => Math.Min(UnitLimit, Entities.Values.Where(e => e.owner == player && e.complete).Sum(e => Type(e).capacity));
        public bool HasRole(int player, string role) => Entities.Values.Any(e => e.owner == player && e.complete && Type(e).role == role);
        public bool Visible(int player, Point position) { int cell = Map.Cell(position); return cell >= 0 && vision[player][cell] == 2; }
        public bool Explored(int player, Point position) { int cell = Map.Cell(position); return cell >= 0 && vision[player][cell] > 0; }
        public bool CanAfford(int player, int minerals, int catalyst) => Players[player].minerals >= minerals && Players[player].catalyst >= catalyst;

        public CommandResult Submit(GameCommand command)
        {
            if (command == null || command.player < 0 || command.player > 1) return Reject("Invalid player.");
            if (command.replacePendingSequence < 0) return Reject("Invalid action sequence.");
            if (Winner != -1 || Paused) return Reject("The match is not active.");
            if (command.sequence > 0 && command.sequence <= lastSequences[command.player]) return Reject("Duplicate command.");
            if (command.sequence > 0) lastSequences[command.player] = command.sequence;
            if (command.kind == CommandKind.Surrender) { Winner = 1 - command.player; return Accept(); }
            if (command.entities == null || command.entities.Length == 0 || command.entities.Length > UnitLimit) return Reject("Select a unit or building.");
            var selected = new List<EntityState>();
            foreach (int id in command.entities.Distinct())
            {
                if (!Entities.TryGetValue(id, out var entity) || entity.owner != command.player) return Reject("You do not own the selected units or buildings.");
                selected.Add(entity);
            }
            if (IsUnitAction(command.kind) && (command.append || command.replacePendingSequence > 0))
                return QueueCommand(command, selected);
            return ExecuteCommand(command, selected, false);
        }
        private CommandResult ExecuteCommand(GameCommand command, List<EntityState> selected, bool preserveQueue)
        {
            EntityState first = selected[0];
            if (command.kind == CommandKind.Build)
            {
                var result = Build(command, first);
                if (result.accepted) MarkUnitOrder(first, command.sequence, preserveQueue);
                return result;
            }
            if (command.kind == CommandKind.Train || command.kind == CommandKind.Research) return Produce(command, first);
            if (command.kind == CommandKind.Cancel) return Cancel(first, command.queueOrderId);
            if (command.kind == CommandKind.SetRallyPoint || command.kind == CommandKind.ClearRallyPoint) return ConfigureRallyPoint(command, selected);
            if (selected.Any(e => !e.complete || Type(e).building)) return Reject("Select completed units or buildings.");
            if (command.kind == CommandKind.Move || command.kind == CommandKind.AttackMove)
            {
                var plan = PlanMovement(command, selected, false);
                if (plan == null) return Reject("Destination outside the map.");
                int accepted = 0;
                foreach (var item in plan) if (ActivateMovement(item.unit, item.order, preserveQueue, item.path).accepted) accepted++;
                return accepted > 0 ? Accept() : Reject("No path to that destination.");
            }
            if (command.kind == CommandKind.Attack || command.kind == CommandKind.Repair)
            {
                if (!Entities.TryGetValue(command.target, out var target)) return Reject("Target does not exist.");
                if (command.kind == CommandKind.Attack && (target.owner == command.player || !Visible(command.player, Center(target)))) return Reject("No visible enemy at that location.");
                if (command.kind == CommandKind.Repair && (target.owner != command.player || Catalog.Faction(Players[command.player].faction).regenerates || selected.Any(e => Type(e).role != "worker"))) return Reject("Only industrial workers can repair allies.");
            }
            else if (command.kind == CommandKind.Gather)
            {
                var resource = Resources.FirstOrDefault(r => r.id == command.target);
                if (resource == null || !Explored(command.player, resource.position) || selected.Any(e => Type(e).role != "worker")) return Reject("Select workers and an explored resource deposit.");
                if (resource.kind == "catalyst" && !Extractor(command.player, resource)) return Reject("Build an extractor over the catalyst deposit.");
                var result = AssignGather(selected, resource);
                if (result.accepted) foreach (var entity in selected) MarkUnitOrder(entity, command.sequence, preserveQueue);
                return result;
            }
            else if (command.kind != CommandKind.Stop && command.kind != CommandKind.Hold) return Reject("Unknown command.");
            foreach (var entity in selected)
            {
                MarkUnitOrder(entity, command.sequence, preserveQueue);
                ResetTravel(entity);entity.order = command.kind; entity.target = command.target;
                entity.braking=command.kind==CommandKind.Stop&&entity.velocity.Distance(default)>.01f;
                if (command.kind == CommandKind.Repair && !Entities[command.target].complete) entity.order = CommandKind.Build;
            }
            return Accept();
        }
        private CommandResult Build(GameCommand command, EntityState worker)
        {
            if (!worker.complete || Type(worker).role != "worker" || !Catalog.Has(command.definition)) return Reject("Select a worker.");
            var validation = ValidateBuildingOrder(command, worker, false);
            if (!validation.accepted) return validation;
            var type = Catalog[command.definition]; int player = worker.owner;
            Point position = new Point((float)Math.Floor(command.position.x), (float)Math.Floor(command.position.y));
            Pay(player, type.minerals, type.catalyst);
            var building = Spawn(player, type.id, position); building.complete = false; building.remaining = type.duration; building.health = type.health * .15f;
            ResetTravel(worker);worker.order = CommandKind.Build; worker.target = building.id;
            return Accept();
        }
        private CommandResult ValidateBuildingOrder(GameCommand command, EntityState worker, bool deferred)
        {
            if (!worker.complete || Type(worker).role != "worker" || !Catalog.Has(command.definition)) return Reject("Select a worker.");
            var type = Catalog[command.definition]; int player = worker.owner;
            if (!type.building || type.faction != Players[player].faction) return Reject("Invalid building.");
            if (!Finite(command.position)) return Reject("Invalid position.");
            Point position = new Point((float)Math.Floor(command.position.x), (float)Math.Floor(command.position.y));
            if (Buildings(player) >= BuildingLimit) return Reject("Building limit reached (20).");
            if (!deferred && !string.IsNullOrEmpty(type.requires) && !HasRole(player, type.requires)) return Reject("Required building missing.");
            if (!deferred && !CanAfford(player, type.minerals, type.catalyst)) return Reject("Not enough resources.");
            if (!Map.CanPlace(position, type.footprint)) return Reject("Cannot build on this terrain.");
            for (int y = (int)position.y; y < position.y + type.footprint; y++) for (int x = (int)position.x; x < position.x + type.footprint; x++)
                if (!Visible(player, new Point(x + .5f, y + .5f))) return Reject("You need vision of the construction area.");
            var covered = Resources.Where(r => r.remaining > 0 && InsideFootprint(r.position, position, type.footprint)).ToArray();
            if (type.role == "extractor")
            { if (covered.Length != 1 || covered[0].kind != "catalyst") return Reject("The extractor must cover a catalyst deposit."); }
            else if (covered.Length > 0) return Reject("Resources block that location.");
            if (!deferred && Entities.Values.Any(e => !Type(e).building && e.health>0 && OverlapsFootprint(e.position,Type(e).collisionRadius,position,type.footprint))) return Reject("Units block that location.");
            var center = new Point(position.x + type.footprint / 2f, position.y + type.footprint / 2f);
            if (paths.Find(worker.position, center, type.footprint * .75f + 1.2f) == null) return Reject("The worker cannot reach that location.");
            return Accept();
        }
        private CommandResult Produce(GameCommand command, EntityState producer)
        {
            int player = producer.owner;
            if (!producer.complete || !Type(producer).building || producer.queue.Count >= 5) return Reject("Building unavailable or queue full.");
            if (command.kind == CommandKind.Research)
            {
                var upgrade = Catalog.Upgrade(command.definition);
                if (upgrade == null || Type(producer).role != "tech") return Reject("Invalid upgrade.");
                if ((upgrade.id == "attack" ? Players[player].attackLevel : Players[player].armorLevel) > 0 || Entities.Values.Any(e => e.owner == player && e.queue.Any(q => q.research && q.definition == upgrade.id))) return Reject("Upgrade already researched or queued.");
                if (!CanAfford(player, upgrade.minerals, upgrade.catalyst)) return Reject("Not enough resources.");
                Pay(player, upgrade.minerals, upgrade.catalyst);
                producer.queue.Add(new ProductionOrder { id = nextProductionOrderIds[player]++, definition = upgrade.id, research = true, remaining = upgrade.duration, total = upgrade.duration });
            }
            else
            {
                if (!Catalog.Has(command.definition)) return Reject("Unknown unit.");
                var type = Catalog[command.definition];
                if (type.building || type.faction != Players[player].faction || type.producer != Type(producer).role) return Reject("This building cannot train that unit.");
                if (!string.IsNullOrEmpty(type.requires) && !HasRole(player, type.requires)) return Reject("Required technology missing.");
                if (Units(player) + Reserved(player) >= Capacity(player)) return Reject("You need more supply.");
                if (!CanAfford(player, type.minerals, type.catalyst)) return Reject("Not enough resources.");
                Pay(player, type.minerals, type.catalyst);
                producer.queue.Add(new ProductionOrder { id = nextProductionOrderIds[player]++, definition = type.id, remaining = type.duration, total = type.duration });
            }
            return Accept();
        }
        private CommandResult Cancel(EntityState entity, int queueOrderId)
        {
            if (queueOrderId < 0) return Reject("Invalid production order.");
            if (!entity.complete)
            {
                if (queueOrderId != 0) return Reject("Production order no longer available.");
                var type = Type(entity); Pay(entity.owner, -(int)(type.minerals * .75f), -(int)(type.catalyst * .75f)); Remove(entity); return Accept();
            }
            if (entity.queue.Count == 0) return Reject("Nothing in the queue to cancel.");
            int index = queueOrderId == 0 ? entity.queue.Count - 1 : entity.queue.FindIndex(q => q.id == queueOrderId);
            if (index < 0) return Reject("Production order no longer available.");
            var order = entity.queue[index]; entity.queue.RemoveAt(index);
            if (order.research) { var u = Catalog.Upgrade(order.definition); Pay(entity.owner, -u.minerals, -u.catalyst); }
            else { var t = Catalog[order.definition]; Pay(entity.owner, -t.minerals, -t.catalyst); }
            return Accept();
        }
        public void Step()
        {
            if (Paused || Winner != -1) return;
            Tick++; recentAttacks.RemoveAll(a=>Tick-a.shot.tick>=TickRate); Array.Clear(unitCells, 0, unitCells.Length);
            foreach (var e in Entities.Values) if (!Type(e).building) { int c = Map.Cell(e.position); if (c >= 0) unitCells[c]++; }
            var active = Entities.Values.OrderBy(e => e.id).ToArray();
            BeginMovementStep(active);
            for(int index=0;index<active.Length;index++)
            {
                var e=active[(index+Tick%active.Length)%active.Length];
                if (e.health <= 0 || !Entities.ContainsKey(e.id)) continue;
                var type = Type(e);
                if (!e.complete) { AdvanceConstruction(e); continue; }
                if (Catalog.Faction(type.faction).regenerates && Tick - e.lastDamageTick > 120) e.health = Math.Min(type.health, e.health + type.health * .00015f);
                if (type.building) { AdvanceProduction(e); if (type.damage > 0) Fight(e, false); continue; }
                if(e.yieldUntil>Tick){Travel(e,e.position,0);continue;}
                if(e.braking){Brake(e);continue;}
                if(e.order==CommandKind.Move&&Tick<e.resumeMoveTick)continue;
                switch (e.order)
                {
                    case CommandKind.Build: Construct(e); break;
                    case CommandKind.Gather: Gather(e); break;
                    case CommandKind.Repair: Repair(e); break;
                    case CommandKind.Move: if (Travel(e, e.destination, .1f)) {e.order = CommandKind.Stop;e.settled=true;} break;
                    case CommandKind.Attack: Attack(e); break;
                    case CommandKind.AttackMove: if (!Fight(e, true) && Travel(e, e.destination, .1f)) e.order = CommandKind.Stop; break;
                    default: if (type.role != "worker") Fight(e, e.order != CommandKind.Hold); break;
                }
                if (e.order == CommandKind.Stop) AdvanceUnitOrders(e);
            }
            EndMovementStep();
            foreach (var e in active) if (e.health <= 0 && Entities.ContainsKey(e.id)) Remove(e);
            if (Tick % 4 == 0) UpdateVision();
            bool a = Buildings(0) > 0, b = Buildings(1) > 0;
            if (!a || !b) Winner = !a && !b ? 2 : a ? 0 : 1;
        }
        private EntityState Spawn(int player, string definition, Point position)
        {
            var type = Catalog[definition];
            var e = new EntityState { id = nextId++, owner = player, definition = definition, position = position, health = type.health };
            Entities.Add(e.id, e);
            if (type.building) Map.Occupy(position, type.footprint, e.id);
            else { int c = Map.Cell(position); if (c >= 0) unitCells[c]++; e.stepOrigin=position;mobiles.Add(e); }
            return e;
        }
        private void Remove(EntityState e) { if (Type(e).building) Map.Occupy(e.position, Type(e).footprint, 0); Entities.Remove(e.id); }
        private void Pay(int player, int minerals, int catalyst) { Players[player].minerals -= minerals; Players[player].catalyst -= catalyst; }
        private static bool Finite(Point p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y);
        private static bool InsideFootprint(Point p, Point origin, int size) => p.x >= origin.x && p.x < origin.x + size && p.y >= origin.y && p.y < origin.y + size;
        private static bool OverlapsFootprint(Point p,float radius,Point origin,int size)
        {
            var closest=new Point(Math.Max(origin.x,Math.Min(origin.x+size,p.x)),Math.Max(origin.y,Math.Min(origin.y+size,p.y)));
            return p.Distance(closest)<radius;
        }
        private static CommandResult Accept() => new CommandResult(true);
        private static CommandResult Reject(string reason) => new CommandResult(false, reason);
        private bool Extractor(int player, ResourceState resource) => Entities.Values.Any(e => e.owner == player && e.health > 0 && e.complete && Type(e).role == "extractor" && InsideFootprint(resource.position, e.position, Type(e).footprint));
    }
}
