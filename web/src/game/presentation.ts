import type { PlayerView, Point, VisibleEntity } from '../protocol/types';
import { TickRate } from './catalog';

// Port of OnlinePresentation: cosmetic clocks only. Positions are interpolated inside a
// short buffer of observed states, never extrapolated; entity lifetime, visibility and
// orders always use the newest PlayerView.
export class Presentation {
  static readonly BufferTicks = TickRate * 0.15;
  static readonly ResetGapTicks = 6;
  private static readonly MaxSnapshots = 8;
  private snapshots: { tick: number; positions: Map<number, Point> }[] = [];
  private appearances = new Map<number, { tick: number; position: Point }>();
  private lastTime = 0;
  private finished = false;
  animationTick = 0;
  positionTick = 0;
  latestTick = 0;

  constructor(private readonly bufferTicks = Presentation.BufferTicks) {}

  reset(): void {
    this.snapshots = []; this.appearances.clear(); this.finished = false; this.lastTime = 0;
    this.animationTick = this.positionTick = this.latestTick = 0;
  }

  receive(view: PlayerView, now: number, paused = false): void {
    if (this.snapshots.length > 0 && view.tick < this.latestTick) return;
    this.advance(now, paused);
    const restart = this.snapshots.length === 0 || view.tick - this.latestTick > Presentation.ResetGapTicks;
    if (restart) { this.snapshots = []; this.appearances.clear(); this.positionTick = view.tick - this.bufferTicks; this.lastTime = now; }
    const sample = { tick: view.tick, positions: new Map<number, Point>() };
    const visible = new Map<number, { tick: number; position: Point }>();
    for (const entity of view.entities) {
      if (entity.remembered) continue;
      sample.positions.set(entity.id, entity.position);
      visible.set(entity.id, this.appearances.get(entity.id) ?? { tick: view.tick, position: entity.position });
    }
    this.appearances = visible;
    const last = this.snapshots[this.snapshots.length - 1];
    if (last && last.tick === view.tick) this.snapshots[this.snapshots.length - 1] = sample; else this.snapshots.push(sample);
    this.latestTick = view.tick; this.finished = view.winner >= 0;
    this.animationTick = Math.max(this.animationTick, this.latestTick);
    while (this.snapshots.length > 2 && this.snapshots[1].tick <= this.positionTick) this.snapshots.shift();
    while (this.snapshots.length > Presentation.MaxSnapshots) this.snapshots.shift();
  }

  advance(now: number, paused = false): void {
    if (this.snapshots.length === 0) return;
    const elapsed = Math.max(0, now - this.lastTime);
    this.lastTime = Math.max(this.lastTime, now);
    if (paused) return;
    const ticks = elapsed * TickRate;
    this.animationTick = Math.min(this.latestTick + (this.finished ? TickRate : this.bufferTicks), this.animationTick + ticks);
    const rate = this.latestTick - this.positionTick > this.bufferTicks + 0.001 ? 1.1 : 1;
    this.positionTick = Math.min(this.latestTick, this.positionTick + ticks * rate);
  }

  position(entity: VisibleEntity): Point {
    const appearance = this.appearances.get(entity.id);
    if (entity.remembered || this.snapshots.length === 0 || !appearance) return entity.position;
    if (this.positionTick < appearance.tick) return appearance.position;
    let lower = 0;
    while (lower + 1 < this.snapshots.length && this.snapshots[lower + 1].tick <= this.positionTick) lower++;
    const start = this.snapshots[lower].positions.get(entity.id);
    if (!start) return entity.position;
    if (lower + 1 === this.snapshots.length) return start;
    const next = this.snapshots[lower + 1];
    const end = next.positions.get(entity.id);
    if (!end) return entity.position;
    const t = Math.max(0, Math.min(1, (this.positionTick - this.snapshots[lower].tick) / (next.tick - this.snapshots[lower].tick)));
    return { x: start.x + (end.x - start.x) * t, y: start.y + (end.y - start.y) * t };
  }
}
