using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public static class Harvesting
    {
        public const float SearchRadius = 14, BaseRadius = 18;
        public static int OptimalWorkers(string kind) => kind == "catalyst" ? 3 : 2;
    }

    public sealed partial class Simulation
    {
        private int KnownRemaining(int player, ResourceState resource) => Visible(player, resource.position) ? resource.remaining :
            resourceMemories[player].TryGetValue(resource.id, out var remembered) ? remembered : -1;

        private Dictionary<int, int> GatherAssignments(int player, IEnumerable<int> excluded)
        {
            var skip = new HashSet<int>(excluded);
            var counts = Resources.ToDictionary(r => r.id, r => 0);
            foreach (var worker in Entities.Values)
                if (worker.owner == player && worker.health > 0 && worker.order == CommandKind.Gather && !skip.Contains(worker.id) && counts.ContainsKey(worker.resourceId))
                    counts[worker.resourceId]++;
            return counts;
        }

        private ResourceState FindGatherResource(EntityState worker, string kind, Point origin, int preferred,
            Dictionary<int, int> assigned, bool onlyFree = false, bool local = true)
        {
            var candidates = Resources.Where(r => r.kind == kind && Explored(worker.owner, r.position) && KnownRemaining(worker.owner, r) != 0
                && (!local || r.position.Distance(origin) <= Harvesting.SearchRadius)
                && (kind != "catalyst" || Extractor(worker.owner, r))
                && (!onlyFree || assigned[r.id] < Harvesting.OptimalWorkers(r.kind)))
                .OrderBy(r => assigned[r.id] >= Harvesting.OptimalWorkers(r.kind) ? 1 : 0)
                .ThenBy(r => r.id == preferred && assigned[r.id] < Harvesting.OptimalWorkers(r.kind) ? 0 : 1)
                .ThenBy(r => assigned[r.id])
                .ThenBy(r => r.position.Distance(origin))
                .ThenBy(r => r.id);
            foreach (var resource in candidates)
                if (paths.Find(worker.position, resource.position, GatherRange(resource), null, Type(worker).collisionRadius) != null)
                    return resource;
            return null;
        }

        private static float GatherRange(ResourceState resource) => resource.kind == "catalyst" ? 3f : 1.5f;

        private void SetGather(EntityState worker, ResourceState resource)
        {
            ResetTravel(worker);
            worker.order = CommandKind.Gather;
            worker.resourceId = worker.target = resource.id;
            worker.depositing = worker.cargo > 0;
        }

        private CommandResult AssignGather(List<EntityState> workers, ResourceState requested)
        {
            // Reserve every assignment before the next worker is evaluated, including a group order.
            // Do not mutate orders until all workers have a reachable destination.
            var assigned = GatherAssignments(workers[0].owner, workers.Select(w => w.id));
            var plan = new List<(EntityState worker, ResourceState resource)>();
            foreach (var worker in workers.OrderBy(w => w.id))
            {
                var resource = FindGatherResource(worker, requested.kind, requested.position, requested.id, assigned);
                if (resource == null) return Reject("No reachable deposits of that resource near the target.");
                plan.Add((worker, resource)); assigned[resource.id]++;
            }
            foreach (var item in plan) SetGather(item.worker, item.resource);
            return Accept();
        }

        private void ResumeGather(EntityState worker)
        {
            var assigned = GatherAssignments(worker.owner, new[] { worker.id });
            var previous = Resources.FirstOrDefault(r => r.id == worker.resourceId);
            var resource = previous == null ? null : FindGatherResource(worker, previous.kind, previous.position, 0, assigned);
            if (resource == null) resource = FindGatherResource(worker, "mineral", worker.position, 0, assigned);
            if (resource == null) resource = FindGatherResource(worker, "mineral", worker.position, 0, assigned, local: false);
            if (resource != null) { SetGather(worker, resource); return; }
            ResetTravel(worker); worker.order = CommandKind.Stop; worker.target = worker.resourceId = 0;
        }

        private void RebalanceGather(EntityState worker, ResourceState resource)
        {
            var assigned = GatherAssignments(worker.owner, new[] { worker.id });
            if (assigned[resource.id] < Harvesting.OptimalWorkers(resource.kind)) return;
            var replacement = FindGatherResource(worker, resource.kind, resource.position, 0, assigned, onlyFree: true);
            if (replacement != null) SetGather(worker, replacement);
        }

        private ResourceState ObserveResource(int player, ResourceState resource, Dictionary<int, int> assigned)
        {
            int remaining = KnownRemaining(player, resource);
            var extractor = resource.kind != "catalyst" ? null : Entities.Values.FirstOrDefault(e => e.owner == player && e.health > 0 && e.complete && Type(e).role == "extractor"
                && InsideFootprint(resource.position, e.position, Type(e).footprint));
            var hq = Entities.Values.Where(e => e.owner == player && e.health > 0 && e.complete && Type(e).role == "hq" && Center(e).Distance(resource.position) <= Harvesting.BaseRadius)
                .OrderBy(e => Center(e).Distance(resource.position)).ThenBy(e => e.id).FirstOrDefault();
            return new ResourceState { id = resource.id, definition = resource.definition, kind = resource.kind, position = resource.position, remaining = remaining,
                assignedWorkers = assigned[resource.id], optimalWorkers = remaining == 0 || (resource.kind == "catalyst" && extractor == null) ? 0 : Harvesting.OptimalWorkers(resource.kind),
                baseId = hq?.id ?? 0, extractorId = extractor?.id ?? 0 };
        }
    }
}
