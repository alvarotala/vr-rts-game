import { describe, expect, it } from 'vitest';
import { catalogText, mapTexts } from 'virtual:frontier-content';
import { Catalog } from '../../src/game/catalog';
import { Terrain, parseMap } from '../../src/game/terrain';
import { PlacementValidity } from '../../src/game/placement';
import { Presentation } from '../../src/game/presentation';
import { normalizeView, type PlayerView } from '../../src/protocol/types';
import { fixture } from '../fixture';

describe('construction preview (BuildPlacementValidity port)', () => {
  const catalog = new Catalog(catalogText);
  const terrain = new Terrain(parseMap(mapTexts.rift));
  const view = normalizeView(JSON.parse(fixture.viewJson));
  const mask = new PlacementValidity(terrain).mask(view, catalog, 'industrial.supply', 2);
  const at = (x: number, y: number) => mask[y * terrain.width + x];
  it('blocks the headquarters footprint, deposits and unexplored ground', () => {
    const hq = view.entities.find((e) => e.definition === 'industrial.hq' && e.owner === 0)!;
    expect(at(hq.position.x + 1, hq.position.y + 1)).toBe(0);
    const mineral = view.resources.find((r) => r.kind === 'mineral')!;
    expect(at(Math.floor(mineral.position.x), Math.floor(mineral.position.y))).toBe(0);
    expect(at(120, 120)).toBe(0);
  });
  it('accepts visible open ground near the base', () => {
    let valid = 0;
    for (let y = 0; y < 40; y++) for (let x = 0; x < 40; x++) valid += at(x, y);
    expect(valid).toBeGreaterThan(20);
  });
});

describe('position buffer (OnlinePresentation port)', () => {
  const view = (tick: number, x: number): PlayerView => normalizeView({ player: 0, tick, winner: -1, entities: [{ id: 1, owner: 0, definition: 'industrial.worker', position: { x, y: 0 } }], visibility: [] });
  it('interpolates inside the buffer and never extrapolates', () => {
    const presentation = new Presentation();
    presentation.receive(view(10, 0), 0);
    presentation.receive(view(12, 2), 0.1);
    presentation.advance(0.1);
    const mid = presentation.position(view(12, 2).entities[0]);
    expect(mid.x).toBeGreaterThanOrEqual(0); expect(mid.x).toBeLessThanOrEqual(2);
    presentation.advance(5);
    expect(presentation.position(view(12, 2).entities[0]).x).toBe(2);
  });
});
