import type { Point } from '../protocol/types';

export interface TerrainRegion { x: number; y: number; width: number; height: number; elevation: number; walkable: boolean; buildable: boolean; ramp: boolean; rampAxis: string; rampDirection: number }
export interface MapDefinition {
  schemaVersion: number; id: string; title: string; theme: string; width: number; height: number;
  terrain: TerrainRegion[]; spawns: { player: number; x: number; y: number }[];
  resources: { id: string; kind: string; x: number; y: number; amount: number }[];
  decoration: { id: string; x: number; y: number; rotation: number }[];
}

export function parseMap(json: string): MapDefinition {
  const raw = JSON.parse(json) as Partial<MapDefinition>;
  return {
    schemaVersion: raw.schemaVersion ?? 1, id: raw.id ?? '', title: raw.title ?? '', theme: raw.theme ?? 'temperate',
    width: raw.width ?? 128, height: raw.height ?? 128,
    terrain: (raw.terrain ?? []).map((r: Partial<TerrainRegion>) => ({ x: r.x ?? 0, y: r.y ?? 0, width: r.width ?? 1, height: r.height ?? 1, elevation: r.elevation ?? 0,
      walkable: r.walkable ?? true, buildable: r.buildable ?? true, ramp: r.ramp ?? false, rampAxis: r.rampAxis ?? 'x', rampDirection: r.rampDirection ?? 1 })),
    spawns: raw.spawns ?? [], resources: raw.resources ?? [], decoration: raw.decoration ?? [],
  };
}

// Port of GridMap's terrain decoding (regions applied in order) plus BoardView's
// continuous ramp surface, used for rendering, picking and build previews.
export class Terrain {
  readonly width: number;
  readonly height: number;
  readonly walkable: Uint8Array;
  readonly buildable: Uint8Array;
  readonly ramp: Uint8Array;
  readonly elevation: Uint8Array;
  readonly surface: Float32Array;
  private readonly ramps: (TerrainRegion | null)[];
  readonly rise: number;

  constructor(readonly map: MapDefinition) {
    this.width = map.width; this.height = map.height;
    const count = this.width * this.height;
    this.walkable = new Uint8Array(count).fill(1); this.buildable = new Uint8Array(count).fill(1);
    this.ramp = new Uint8Array(count); this.elevation = new Uint8Array(count); this.surface = new Float32Array(count);
    this.ramps = new Array(count).fill(null);
    for (const region of map.terrain) {
      for (let y = region.y; y < region.y + region.height; y++) for (let x = region.x; x < region.x + region.width; x++) {
        if (!this.inside(x, y)) continue;
        const i = y * this.width + x;
        this.walkable[i] = region.walkable ? 1 : 0; this.buildable[i] = region.buildable ? 1 : 0; this.ramp[i] = region.ramp ? 1 : 0;
        this.elevation[i] = region.elevation; this.surface[i] = region.elevation;
        this.ramps[i] = region.ramp ? region : null;
        if (region.ramp) {
          const fraction = region.rampAxis === 'x' ? (x - region.x + 0.5) / region.width : (y - region.y + 0.5) / region.height;
          this.surface[i] = region.rampDirection > 0 ? fraction : 1 - fraction;
        }
      }
    }
    this.rise = map.theme === 'desert' ? 3 : 1.5;
  }

  inside(x: number, y: number): boolean { return x >= 0 && y >= 0 && x < this.width && y < this.height; }
  index(x: number, y: number): number { return y * this.width + x; }
  cell(p: Point): number {
    const x = Math.floor(p.x), y = Math.floor(p.y);
    return this.inside(x, y) ? this.index(x, y) : -1;
  }

  // Height of the rendered surface inside one cell, matching BoardView.SurfaceHeight.
  surfaceHeight(cell: number, x: number, y: number): number {
    const region = this.ramps[cell];
    if (!region) return this.surface[cell] * this.rise;
    const t = region.rampAxis === 'x' ? (x - region.x) / region.width : (y - region.y) / region.height;
    return (region.rampDirection > 0 ? t : 1 - t) * this.rise;
  }

  heightAt(x: number, y: number): number {
    const cx = Math.min(this.width - 1, Math.max(0, Math.floor(x))), cy = Math.min(this.height - 1, Math.max(0, Math.floor(y)));
    return this.surfaceHeight(this.index(cx, cy), x, y);
  }

  hasLineOfSight(from: Point, to: Point): boolean {
    const a = this.cell(from), b = this.cell(to);
    if (a < 0 || b < 0) return false;
    const steps = Math.max(1, Math.ceil(Math.hypot(from.x - to.x, from.y - to.y) * 2));
    for (let s = 1; s <= steps; s++) {
      const t = s / steps;
      const i = this.cell({ x: from.x + (to.x - from.x) * t, y: from.y + (to.y - from.y) * t });
      if (i < 0 || (this.elevation[i] > this.elevation[a] && !this.ramp[i]) || !this.walkable[i]) return false;
    }
    return true;
  }
}
