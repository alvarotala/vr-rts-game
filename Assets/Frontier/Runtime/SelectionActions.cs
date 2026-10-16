using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;

namespace Frontier.Runtime
{
    // One source of contextual commands for both the console and the controller wheel.
    public sealed class SelectionAction
    {
        public string Id, Title, Label, Detail;
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
                        result.Add("train:" + type.id, "Train " + type.name, type.name, UnitVisuals.RoleName(type.role) + " · " + Cost(type.minerals, type.catalyst), () => session.Send(CommandKind.Train, captured.id));
                    }
                    if (role == "tech") foreach (var upgrade in session.Catalog.Definition.upgrades)
                    {
                        var captured = upgrade;
                        result.Add("research:" + upgrade.id, upgrade.name, upgrade.name, "Research · " + Cost(upgrade.minerals, upgrade.catalyst), () => session.Send(CommandKind.Research, captured.id));
                    }
                }
                if (!entity.complete || entity.queue.Length > 0)
                    result.Add("cancel", "Cancel / refund", "Cancel / refund", entity.complete ? "Last queued item" : "Cancel construction", () => session.Send(CommandKind.Cancel));
                if (result.Items.Count == 0) result.Hint = "This building operates automatically.";
            }
            else if (selected.Length > 0 && selected.All(e => !session.Catalog[e.definition].building))
            {
                bool workers = selected.All(e => session.Catalog[e.definition].role == "worker");
                result.Heading = workers ? "BUILD & UNIT ORDERS" : "UNIT ORDERS";
                if (workers) foreach (var type in session.Catalog.Definition.archetypes.Where(t => t.faction == session.Faction && t.building))
                {
                    var captured = type;
                    result.Add("build:" + type.id, "Build " + type.name, type.name, Cost(type.minerals, type.catalyst), () => { session.ChooseBuild(captured.id); return true; }, true);
                }
                result.Add("attack-move", "Attack Move", "Attack Move", "Choose a destination", () => { session.CancelMode(); session.AttackMoveMode = true; session.Announce("Confirm the attack destination."); return true; });
                result.Add("stop", "Stop", "Stop", "Cancel current orders", () => UnitOrder(session, CommandKind.Stop));
                result.Add("hold", "Hold Position", "Hold Position", "Defend this position", () => UnitOrder(session, CommandKind.Hold));
            }
            else if (selected.Length > 0) result.Hint = "Select mobile units or a single building to give orders.";
            return result;
        }

        private void Add(string id, string title, string label, string detail, Func<bool> execute, bool building = false)
            => Items.Add(new SelectionAction { Id = id, Title = title, Label = label, Detail = detail, Execute = execute, Building = building });
        private static bool UnitOrder(GameSession session, CommandKind kind)
        {
            if (!session.Send(kind)) return false;
            session.CancelMode(); return true;
        }
        private static string Cost(int minerals, int gas) => minerals + " M" + (gas > 0 ? " / " + gas + " G" : "");
    }
}
