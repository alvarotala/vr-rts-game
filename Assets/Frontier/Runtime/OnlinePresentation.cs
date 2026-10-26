using System;
using System.Collections.Generic;
using Frontier.Core;

namespace Frontier.Runtime
{
    // Cosmetic clocks only. State, events and entity lifetime still use the newest
    // filtered PlayerView; no world simulation or hidden positions are retained.
    public sealed class OnlinePresentation
    {
        public const float BufferTicks = Simulation.TickRate * .15f;
        public const int ResetGapTicks = 6;
        private const int MaxSnapshots = 8;
        private sealed class Snapshot
        {
            public int tick;
            public readonly Dictionary<int, Point> positions = new Dictionary<int, Point>();
        }
        private struct Appearance { public int tick; public Point position; }
        private readonly List<Snapshot> snapshots = new List<Snapshot>();
        private Dictionary<int, Appearance> appearances = new Dictionary<int, Appearance>();
        private double lastTime;
        private bool finished;
        public float AnimationTick { get; private set; }
        public float PositionTick { get; private set; }
        public int LatestTick { get; private set; }

        public void Reset()
        {
            snapshots.Clear(); appearances.Clear(); finished = false; lastTime = 0;
            AnimationTick = PositionTick = LatestTick = 0;
        }

        public void Receive(PlayerView view, double now, bool paused = false)
        {
            if (snapshots.Count > 0 && view.tick < LatestTick) return;
            Advance(now, paused);
            bool restart = snapshots.Count == 0 || view.tick - LatestTick > ResetGapTicks;
            if (restart)
            {
                snapshots.Clear(); appearances.Clear(); PositionTick = view.tick - BufferTicks;
                lastTime = now;
            }
            var sample = new Snapshot { tick = view.tick };
            var visible = new Dictionary<int, Appearance>();
            foreach (var entity in view.entities)
            {
                if (entity.remembered) continue;
                sample.positions[entity.id] = entity.position;
                visible[entity.id] = appearances.TryGetValue(entity.id, out var appearance)
                    ? appearance : new Appearance { tick = view.tick, position = entity.position };
            }
            appearances = visible;
            if (snapshots.Count > 0 && snapshots[snapshots.Count - 1].tick == view.tick)
                snapshots[snapshots.Count - 1] = sample;
            else snapshots.Add(sample);
            LatestTick = view.tick; finished = view.winner >= 0;
            // An early packet can bring a new event forward; a late packet never
            // rewinds an animation which has already played between snapshots.
            AnimationTick = Math.Max(AnimationTick, LatestTick);
            while (snapshots.Count > 2 && snapshots[1].tick <= PositionTick) snapshots.RemoveAt(0);
            while (snapshots.Count > MaxSnapshots) snapshots.RemoveAt(0);
        }

        public void Advance(double now, bool paused = false)
        {
            if (snapshots.Count == 0) return;
            float elapsed = (float)Math.Max(0, now - lastTime);
            lastTime = Math.Max(lastTime, now);
            if (paused) return;
            float ticks = elapsed * Simulation.TickRate;
            // Work/weapon cycles may run for one snapshot plus jitter, then stop
            // on a stalled connection. Final effects get their existing one second.
            AnimationTick = Math.Min(LatestTick + (finished ? Simulation.TickRate : BufferTicks), AnimationTick + ticks);
            // A persistent timeline absorbs arrival jitter. Recover small excess
            // latency gently after a lost snapshot, without restarting each lerp.
            float rate = LatestTick - PositionTick > BufferTicks + .001f ? 1.1f : 1;
            PositionTick = Math.Min(LatestTick, PositionTick + ticks * rate);
        }

        public Point Position(VisibleEntity entity)
        {
            if (entity.remembered || snapshots.Count == 0 || !appearances.TryGetValue(entity.id, out var appearance))
                return entity.position;
            // New/reappearing units stay at their first observed position until
            // the buffer catches up, never traversing an earlier hidden route.
            if (PositionTick < appearance.tick) return appearance.position;
            int lower = 0;
            while (lower + 1 < snapshots.Count && snapshots[lower + 1].tick <= PositionTick) lower++;
            if (!snapshots[lower].positions.TryGetValue(entity.id, out var start)) return entity.position;
            if (lower + 1 == snapshots.Count) return start;
            var next = snapshots[lower + 1];
            if (!next.positions.TryGetValue(entity.id, out var end)) return entity.position;
            float t = Math.Max(0, Math.Min(1, (PositionTick - snapshots[lower].tick) / (next.tick - snapshots[lower].tick)));
            return new Point(start.x + (end.x - start.x) * t, start.y + (end.y - start.y) * t);
        }
    }
}
