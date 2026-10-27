import * as THREE from 'three';
import type { Terrain } from '../game/terrain';
import type { Point } from '../protocol/types';

// Ray-marches the heightfield (cheaper and exact enough for a ~100k-triangle board).
export function rayToMap(ray: THREE.Ray, terrain: Terrain): { point: Point; world: THREE.Vector3 } | null {
  const top = terrain.rise + 0.5;
  const o = ray.origin, d = ray.direction;
  if (Math.abs(d.y) < 1e-6) return null;
  // Enter at the slab top, stop below the ground.
  let t = Math.max(0, (top - o.y) / d.y);
  const tEnd = (-0.5 - o.y) / d.y;
  if (tEnd < 0) return null;
  const step = 0.2;
  const p = new THREE.Vector3();
  let previous = t;
  for (; t <= tEnd; t += step) {
    p.copy(d).multiplyScalar(t).add(o);
    const x = p.x, y = -p.z;
    const h = x >= 0 && y >= 0 && x < terrain.width && y < terrain.height ? terrain.heightAt(x, y) : 0;
    if (p.y <= h) {
      let lo = previous, hi = t;
      for (let i = 0; i < 8; i++) {
        const mid = (lo + hi) / 2;
        p.copy(d).multiplyScalar(mid).add(o);
        const mx = p.x, my = -p.z;
        const mh = mx >= 0 && my >= 0 && mx < terrain.width && my < terrain.height ? terrain.heightAt(mx, my) : 0;
        if (p.y <= mh) hi = mid; else lo = mid;
      }
      p.copy(d).multiplyScalar(hi).add(o);
      const point = { x: p.x, y: -p.z };
      if (point.x < 0 || point.y < 0 || point.x >= terrain.width || point.y >= terrain.height) return null;
      return { point, world: p.clone() };
    }
    previous = t;
  }
  return null;
}

// The board plane extended beyond its edges, for drag selection that starts outside.
export function rayToPlane(ray: THREE.Ray): Point | null {
  if (Math.abs(ray.direction.y) < 1e-6) return null;
  const t = -ray.origin.y / ray.direction.y;
  if (t < 0) return null;
  const p = ray.direction.clone().multiplyScalar(t).add(ray.origin);
  return { x: p.x, y: -p.z };
}
