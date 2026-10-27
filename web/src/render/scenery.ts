import * as THREE from 'three';
import type { Terrain } from '../game/terrain';
import { applyFog, type FogOfWar } from './fog';

const random = (x: number, y: number, salt: number) => {
  let h = (x * 73856093) ^ (y * 19349663) ^ (salt * 83492791);
  h = Math.imul(h ^ (h >>> 15), 2246822519); h = Math.imul(h ^ (h >>> 13), 3266489917);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967295;
};

// Decoration on blocked terrain only (trees, cacti, rock spires, neon crystals). Purely
// visual, like ThemedScenery: it never covers a walkable or buildable cell.
export function buildScenery(terrain: Terrain, fog: FogOfWar, theme: string): THREE.Group {
  const group = new THREE.Group();
  group.name = 'scenery';
  const cells: [number, number][] = [];
  for (let y = 0; y < terrain.height; y++) for (let x = 0; x < terrain.width; x++)
    if (!terrain.walkable[terrain.index(x, y)] && random(x, y, 1) < (theme === 'neon' ? 0.22 : 0.42)) cells.push([x, y]);
  if (cells.length === 0) return group;
  const kinds = theme === 'forest'
    ? [{ geometry: new THREE.ConeGeometry(0.42, 1.5, 7), color: 0x2f6b34, y: 1.05, trunk: true }, { geometry: new THREE.ConeGeometry(0.34, 1.15, 6), color: 0x3d7f3c, y: 0.85, trunk: true }]
    : theme === 'desert'
      ? [{ geometry: new THREE.CylinderGeometry(0.16, 0.2, 1.3, 7), color: 0x4f7d3a, y: 0.65, trunk: false }, { geometry: new THREE.DodecahedronGeometry(0.45), color: 0x9c7149, y: 0.25, trunk: false }]
      : theme === 'neon'
        ? [{ geometry: new THREE.OctahedronGeometry(0.4), color: 0x6c4cff, y: 0.6, trunk: false }, { geometry: new THREE.OctahedronGeometry(0.28), color: 0x22d3ee, y: 0.45, trunk: false }]
        : [{ geometry: new THREE.DodecahedronGeometry(0.5), color: 0x5f6b77, y: 0.3, trunk: false }];
  const matrix = new THREE.Matrix4(), quaternion = new THREE.Quaternion(), scale = new THREE.Vector3(), position = new THREE.Vector3();
  kinds.forEach((kind, index) => {
    const chosen = cells.filter(([x, y]) => Math.floor(random(x, y, 2) * kinds.length) === index);
    if (chosen.length === 0) return;
    const material = new THREE.MeshStandardMaterial({ color: kind.color, roughness: 0.85, emissive: theme === 'neon' ? new THREE.Color(kind.color).multiplyScalar(0.45) : new THREE.Color(0) });
    applyFog(material, fog);
    const mesh = new THREE.InstancedMesh(kind.geometry, material, chosen.length);
    const trunkMesh = kind.trunk ? new THREE.InstancedMesh(new THREE.CylinderGeometry(0.07, 0.09, 0.5, 5), (() => { const m = new THREE.MeshStandardMaterial({ color: 0x5b3d25, roughness: 1 }); applyFog(m, fog); return m; })(), chosen.length) : null;
    chosen.forEach(([x, y], i) => {
      const px = x + 0.25 + random(x, y, 3) * 0.5, py = y + 0.25 + random(x, y, 4) * 0.5;
      const s = 0.75 + random(x, y, 5) * 0.6;
      const ground = terrain.heightAt(px, py);
      quaternion.setFromAxisAngle(new THREE.Vector3(0, 1, 0), random(x, y, 6) * Math.PI * 2);
      position.set(px, ground + kind.y * s, -py); scale.setScalar(s);
      matrix.compose(position, quaternion, scale); mesh.setMatrixAt(i, matrix);
      if (trunkMesh) { position.set(px, ground + 0.25 * s, -py); matrix.compose(position, quaternion, scale); trunkMesh.setMatrixAt(i, matrix); }
    });
    mesh.castShadow = true; mesh.receiveShadow = true;
    group.add(mesh);
    if (trunkMesh) { trunkMesh.castShadow = true; group.add(trunkMesh); }
  });
  return group;
}
