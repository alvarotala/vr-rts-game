using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;

namespace Frontier.Runtime
{
    public enum ActionCategory { Build, Train, Research, Orders, Cancel }

    // One source of contextual commands for both the console and the controller wheel.
    public sealed class SelectionAction
    {
        public string Id, Title, Label, Detail, Description, HelpDetail;
        public ActionCategory Category = ActionCategory.Orders;
        public bool Building;
        public Func<bool> Execute;
    }

    public sealed class SelectionActions
    {
        public string Heading = "ACTIONS", Hint = "Select your units or a building to view its available actions.";
        public readonly List<SelectionAction> Items = new List<SelectionAction>();

        public static SelectionActions For(GameSession session)
        {
            var result = new SelectionActions();
            if (session.View == null) return result;
            var selected = session.View.entities.Where(e => e.owner == session.View.player && session.Selection.Contains(e.id)).ToArray();
            if (selected.Length == 1 && session.Catalog[selected[0].definition].building)
            {
                var entity = selected[0]; string role = session.Catalog[entity.definition].role;
                result.Heading = !entity.complete ? "CONSTRUCTION" : role == "tech" ? "TRAIN & RESEARCH" : "BUILDING ACTIONS";
                if (entity.complete)
                {
                    foreach (var type in session.Catalog.Definition.archetypes.Where(t => t.faction == session.Faction && !t.building && t.producer == role))
                    {
                        var captured = type;
                        result.Add("train:" + type.id, "Train " + type.name, type.name, UnitVisuals.RoleName(type.role) + " · " + Cost(type.minerals, type.catalyst), () => session.Send(CommandKind.Train, captured.id), ActionCategory.Train,
                            Describe(type, session.Catalog), Requirements(type, session.Catalog));
                    }
                    if (role == "tech") foreach (var upgrade in session.Catalog.Definition.upgrades)
                    {
                        var captured = upgrade;
                        result.Add("research:" + upgrade.id, upgrade.name, upgrade.name, "Research · " + Cost(upgrade.minerals, upgrade.catalyst), () => session.Send(CommandKind.Research, captured.id), ActionCategory.Research,
                            upgrade.id == "attack" ? "Improves the damage dealt by your army and defensive buildings. Research once to benefit your whole faction." : "Reduces incoming damage to your units and buildings. Research once to protect your whole faction.",
                            Cost(upgrade.minerals, upgrade.catalyst) + " · " + Seconds(upgrade.duration) + " s\nFaction-wide upgrade");
                    }
                }
                if (!entity.complete || entity.queue.Length > 0)
                    result.Add("cancel", "Cancel / refund", "Cancel / refund", entity.complete ? "Last queued item" : "Cancel construction", () => session.Send(CommandKind.Cancel), ActionCategory.Cancel,
                        entity.complete ? "Removes the last item in this building's production queue and refunds its resource cost." : "Abandons this unfinished building and refunds part of its resource cost.");
                if (result.Items.Count == 0) result.Hint = "This building operates automatically.";
            }
            else if (selected.Length > 0 && selected.All(e => !session.Catalog[e.definition].building))
            {
                bool workers = selected.All(e => session.Catalog[e.definition].role == "worker");
                result.Heading = workers ? "BUILD & UNIT ORDERS" : "UNIT ORDERS";
                if (workers) foreach (var type in session.Catalog.Definition.archetypes.Where(t => t.faction == session.Faction && t.building))
                {
                    var captured = type;
                    result.Add("build:" + type.id, "Build " + type.name, type.name, Cost(type.minerals, type.catalyst), () => { session.ChooseBuild(captured.id); return true; }, ActionCategory.Build,
                        Describe(type, session.Catalog), Requirements(type, session.Catalog));
                }
                result.Add("attack-move", "Attack Move", "Attack Move", "Choose a destination", () => { session.CancelMode(); session.AttackMoveMode = true; session.Announce("Confirm the attack destination."); return true; },
                    description: "Choose a point on the battlefield. Selected units advance toward it and engage enemies they spot along the way.");
                result.Add("stop", "Stop", "Stop", "Cancel current orders", () => UnitOrder(session, CommandKind.Stop),
                    description: "Cancels current movement and work orders. Combat units can still spot, pursue and attack nearby enemies.");
                result.Add("hold", "Hold Position", "Hold Position", "Defend this position", () => UnitOrder(session, CommandKind.Hold),
                    description: "Keep selected units at their current positions. Combat units fire within range without chasing enemies.");
            }
            else if (selected.Length > 0) result.Hint = "Select mobile units or a single building to give orders.";
            return result;
        }

        private void Add(string id, string title, string label, string detail, Func<bool> execute, ActionCategory category = ActionCategory.Orders, string description = "", string helpDetail = "")
            => Items.Add(new SelectionAction { Id = id, Title = title, Label = label, Detail = detail, Execute = execute, Building = category == ActionCategory.Build,
                Category = category, Description = description, HelpDetail = helpDetail });

        private static string Describe(Archetype type, Catalog catalog)
        {
            switch (type.role)
            {
                case "worker": return catalog.Faction(type.faction).grows
                    ? "Gathers minerals and gas. Starts growing buildings, then returns to harvesting while they finish on their own."
                    : "Gathers minerals and gas. Stays on site to construct buildings. Can repair damaged allies by spending minerals.";
                case "scout": return "Fast, lightly armed scout. Explore the map, find resources and spot enemy movements before committing your army.";
                case "melee": return "Durable close-range fighter. Use it on the front line to engage enemies and protect your ranged units.";
                case "ranged": return "Ranged attacker. Keep it behind your front line to damage enemies from a safer distance.";
                case "siege": return "Slow, long-range artillery. Heavy shots also damage nearby enemies. Protect it with other units.";
                case "hq": return "Your main base: trains workers and receives gathered resources. Build near new deposits to expand. Adds " + type.capacity + " supply.";
                case "extractor": return "Build directly over a gas deposit so workers can harvest it. Assign workers after completion; " + Harvesting.OptimalWorkers("catalyst") + " is optimal.";
                case "supply": return "Adds " + type.capacity + " supply capacity when complete, allowing you to train more units. It does not produce units or gather resources.";
                case "barracks": return "Trains scouts, close-range fighters and ranged attackers. Build your main army here. Unlocks advanced construction.";
                case "tech": return "Trains long-range artillery and researches faction-wide attack and armor upgrades. Supports a stronger, more versatile army.";
                case "defense": return "Stationary defense that automatically fires at enemies within range. Protect bases and narrow approaches; it cannot move.";
                default: return type.building ? "A structure supporting your base." : "A mobile unit in your army.";
            }
        }
        private static string Requirements(Archetype type, Catalog catalog)
        {
            string info = Cost(type.minerals, type.catalyst) + " · " + Seconds(type.duration) + " s";
            if (!type.building) info += " · " + type.supply + " supply";
            if (!string.IsNullOrEmpty(type.requires)) info += "\nRequires: " + catalog.Role(type.faction, type.requires).name;
            return info;
        }
        private static int Seconds(int ticks) => (ticks + 19) / 20;
        private static bool UnitOrder(GameSession session, CommandKind kind)
        {
            if (!session.Send(kind)) return false;
            session.CancelMode(); return true;
        }
        private static string Cost(int minerals, int gas) => minerals + " M" + (gas > 0 ? " / " + gas + " G" : "");
    }
}
