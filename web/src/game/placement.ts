import type { Catalog } from './catalog';
import type { Terrain } from './terrain';
import type { PlayerView } from '../protocol/types';

// Port of BuildPlacementValidity: spatial preview only, from public terrain and the
// filtered PlayerView. The server still validates cost, requirements and access.
export class PlacementValidity {
  private readonly stride: number;
  private readonly blocked: Int32Array;
  private readonly elevation: Int32Array;
  private readonly resources: Int32Array;
  private readonly gas: Int32Array;
  readonly valid: Uint8Array;
  private cachedView: PlayerView | null = null;
  private cachedDefinition = '';
  private cachedSize = 0;

  constructor(private readonly terrain: Terrain) {
    const { width, height } = terrain;
    this.stride = width + 1;
    const count = this.stride * (height + 1);
    this.blocked = new Int32Array(count); this.elevation = new Int32Array(count); this.resources = new Int32Array(count); this.gas = new Int32Array(count);
    this.valid = new Uint8Array(width * height);
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) this.elevation[(y + 1) * this.stride + x + 1] = terrain.elevation[y * width + x];
    this.prefix(this.elevation);
  }

  mask(view: PlayerView | null, catalog: Catalog, definition: string, size: number): Uint8Array {
    if (view === this.cachedView && definition === this.cachedDefinition && size === this.cachedSize) return this.valid;
    this.cachedView = view; this.cachedDefinition = definition; this.cachedSize = size;
    const { width, height } = this.terrain;
    this.valid.fill(0);
    if (!view || size < 1 || size > width || size > height || view.visibility.length !== this.valid.length) return this.valid;
    this.blocked.fill(0); this.resources.fill(0); this.gas.fill(0);
    const t = this.terrain;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
      const i = y * width + x;
      if (view.visibility[i] !== 2 || !t.buildable[i] || !t.walkable[i] || t.ramp[i]) this.blocked[(y + 1) * this.stride + x + 1] = 1;
    }
    for (const entity of view.entities) {
      if (entity.health <= 0 || !catalog.has(entity.definition)) continue;
      const type = catalog.get(entity.definition);
      if (!type.building) continue;
      const x0 = Math.max(0, Math.floor(entity.position.x)), y0 = Math.max(0, Math.floor(entity.position.y));
      const x1 = Math.min(width, Math.ceil(entity.position.x + type.footprint)), y1 = Math.min(height, Math.ceil(entity.position.y + type.footprint));
      for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) this.blocked[(y + 1) * this.stride + x + 1] = 1;
    }
    for (const resource of view.resources) {
      if (resource.remaining <= 0) continue;
      const cell = t.cell(resource.position);
      if (cell < 0) continue;
      const index = (Math.floor(cell / width) + 1) * this.stride + (cell % width) + 1;
      this.resources[index]++;
      if (resource.kind === 'catalyst') this.gas[index]++;
    }
    this.prefix(this.blocked); this.prefix(this.resources); this.prefix(this.gas);
    const extractor = catalog.has(definition) && catalog.get(definition).role === 'extractor';
    const area = size * size;
    for (let y = 0; y <= height - size; y++) for (let x = 0; x <= width - size; x++) {
      if (this.sum(this.blocked, x, y, size) !== 0) continue;
      const level = this.sum(this.elevation, x, y, size);
      if (level !== 0 && level !== area) continue;
      const deposits = this.sum(this.resources, x, y, size);
      this.valid[y * width + x] = (extractor ? deposits === 1 && this.sum(this.gas, x, y, size) === 1 : deposits === 0) ? 1 : 0;
    }
    for (const entity of view.entities) {
      if (entity.owner === view.player || entity.remembered || entity.health <= 0 || !catalog.has(entity.definition)) continue;
      const type = catalog.get(entity.definition);
      if (type.building) continue;
      const radius = type.collisionRadius, r2 = radius * radius;
      const x0 = Math.max(0, Math.floor(entity.position.x - radius) - size), y0 = Math.max(0, Math.floor(entity.position.y - radius) - size);
      const x1 = Math.min(width - size, Math.ceil(entity.position.x + radius)), y1 = Math.min(height - size, Math.ceil(entity.position.y + radius));
      for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
        const dx = entity.position.x - Math.max(x, Math.min(x + size, entity.position.x));
        const dy = entity.position.y - Math.max(y, Math.min(y + size, entity.position.y));
        if (dx * dx + dy * dy < r2) this.valid[y * width + x] = 0;
      }
    }
    return this.valid;
  }

  private prefix(values: Int32Array) {
    const { width, height } = this.terrain;
    for (let y = 1; y <= height; y++) {
      let row = 0;
      for (let x = 1; x <= width; x++) { const i = y * this.stride + x; row += values[i]; values[i] = values[i - this.stride] + row; }
    }
  }

  private sum(values: Int32Array, x: number, y: number, size: number): number {
    const s = this.stride;
    return values[(y + size) * s + x + size] - values[y * s + x + size] - values[(y + size) * s + x] + values[y * s + x];
  }
}
