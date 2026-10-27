import * as THREE from 'three';
import type { Terrain } from '../game/terrain';
import type { ResourceState } from '../protocol/types';
import { applyFog, type FogOfWar } from './fog';

// Mineral clusters (blue faceted crystals on rock) and gas vents (purple opening with
// vapor), recognizable like ResourceVisuals. Only explored deposits are present in the view.
export class ResourceLayer {
  readonly group = new THREE.Group();
  private readonly visuals = new Map<number, { root: THREE.Group; kind: string; vapor: THREE.Mesh[]; seen: number }>();
  private readonly crystal = new THREE.OctahedronGeometry(0.22, 0);
  private readonly rock = new THREE.DodecahedronGeometry(0.38, 0);
  private readonly ring = new THREE.TorusGeometry(0.62, 0.2, 8, 18);
  private readonly hole = new THREE.CircleGeometry(0.5, 18);
  private readonly puff = new THREE.SphereGeometry(0.22, 8, 6);
  private readonly mineral: THREE.MeshStandardMaterial;
  private readonly stone: THREE.MeshStandardMaterial;
  private readonly vent: THREE.MeshStandardMaterial;
  private readonly glow: THREE.MeshBasicMaterial;
  private readonly vaporMaterial: THREE.MeshBasicMaterial;
  constructor(private readonly terrain: Terrain, fog: FogOfWar) {
    this.group.name = 'resources';
    this.mineral = new THREE.MeshStandardMaterial({ color: 0x53c8ff, emissive: 0x1a6aa8, roughness: 0.25, metalness: 0.1 });
    this.stone = new THREE.MeshStandardMaterial({ color: 0x4b5560, roughness: 0.95 });
    this.vent = new THREE.MeshStandardMaterial({ color: 0x5a5160, roughness: 0.9 });
    this.glow = new THREE.MeshBasicMaterial({ color: 0xb46bff });
    this.vaporMaterial = new THREE.MeshBasicMaterial({ color: 0xd9a8ff, transparent: true, opacity: 0.35, depthWrite: false });
    for (const m of [this.mineral, this.stone, this.vent]) applyFog(m, fog);
    applyFog(this.glow, fog);
  }

  private create(resource: ResourceState) {
    const root = new THREE.Group();
    const vapor: THREE.Mesh[] = [];
    const seed = resource.id * 7919;
    const r = (n: number) => { const v = Math.sin(seed + n * 12.9898) * 43758.5453; return v - Math.floor(v); };
    if (resource.kind === 'catalyst') {
      const ring = new THREE.Mesh(this.ring, this.vent); ring.rotation.x = Math.PI / 2; ring.position.y = 0.12; ring.receiveShadow = true; root.add(ring);
      const hole = new THREE.Mesh(this.hole, this.glow); hole.rotation.x = -Math.PI / 2; hole.position.y = 0.16; root.add(hole);
      for (let i = 0; i < 4; i++) { const puff = new THREE.Mesh(this.puff, this.vaporMaterial); puff.userData.phase = i / 4; root.add(puff); vapor.push(puff); }
    } else {
      const base = new THREE.Mesh(this.rock, this.stone); base.scale.set(1.2, 0.5, 1.2); base.position.y = 0.1; base.receiveShadow = true; root.add(base);
      for (let i = 0; i < 5; i++) {
        const c = new THREE.Mesh(this.crystal, this.mineral);
        const angle = i * 1.256 + r(i) * 0.6, radius = i === 0 ? 0 : 0.22 + r(i + 9) * 0.12;
        c.position.set(Math.cos(angle) * radius, 0.35 + r(i + 3) * 0.2, Math.sin(angle) * radius);
        c.scale.set(0.8, 1.6 + r(i + 5) * 1.4, 0.8);
        c.rotation.set((r(i + 7) - 0.5) * 0.5, r(i) * Math.PI, (r(i + 11) - 0.5) * 0.5);
        c.castShadow = true; root.add(c);
      }
    }
    const height = this.terrain.heightAt(resource.position.x, resource.position.y);
    root.position.set(resource.position.x, height, -resource.position.y);
    root.userData.resourceId = resource.id;
    this.group.add(root);
    return { root, kind: resource.kind, vapor, seen: 0 };
  }

  update(resources: ResourceState[], time: number, frame: number, hiddenVapor: Set<number>): void {
    for (const resource of resources) {
      if (resource.remaining === 0) continue;
      let visual = this.visuals.get(resource.id);
      if (!visual) { visual = this.create(resource); this.visuals.set(resource.id, visual); }
      visual.seen = frame;
      visual.root.visible = true;
      const vaporOn = !hiddenVapor.has(resource.id);
      for (const puff of visual.vapor) {
        puff.visible = vaporOn;
        if (!vaporOn) continue;
        const phase = (time * 0.35 + (puff.userData.phase as number)) % 1;
        puff.position.set(Math.sin(phase * 6 + resource.id) * 0.15, 0.3 + phase * 1.6, Math.cos(phase * 5) * 0.15);
        puff.scale.setScalar(0.6 + phase * 1.4);
      }
    }
    for (const [id, visual] of this.visuals) if (visual.seen !== frame) { this.group.remove(visual.root); this.visuals.delete(id); }
  }

  root(id: number): THREE.Object3D | undefined { return this.visuals.get(id)?.root; }
}
