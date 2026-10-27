import * as THREE from 'three';
import type { Archetype } from '../game/catalog';

// Geometry ported from UnitVisuals/BuildingVisuals: [x, y, z, sizeX, sizeY, sizeZ, material]
// in Unity's model space (+z forward). Material 0 is the team hull, 1 the dark structure and
// 2 the role accent. The Consortium uses boxes; Symbiosis uses organic lobes.
type Part = [number, number, number, number, number, number, number?];
type Shape = { kind: 'part'; part: Part } | { kind: 'prism'; part: Part; sides: number } | { kind: 'tube'; center: [number, number, number]; outer: number; inner: number; height: number; material: number } | { kind: 'lobe'; part: Part };

export const OwnColor = new THREE.Color(0.2, 0.8, 1);
export const EnemyColor = new THREE.Color(1, 0.36, 0.23);
const StructureColor = new THREE.Color(0.12, 0.17, 0.22);

export function unitAccent(role: string): THREE.Color {
  switch (role) {
    case 'worker': return new THREE.Color(1, 0.76, 0.18);
    case 'scout': return new THREE.Color(0.45, 1, 0.65);
    case 'melee': return new THREE.Color(1, 0.86, 0.65);
    case 'ranged': return new THREE.Color(0.9, 0.96, 1);
    case 'caster': return new THREE.Color(0.76, 0.38, 1);
    default: return new THREE.Color(0.84, 0.55, 1);
  }
}
export function buildingAccent(role: string): THREE.Color {
  switch (role) {
    case 'hq': return new THREE.Color(1, 0.85, 0.25);
    case 'barracks': return new THREE.Color(1, 0.55, 0.18);
    case 'extractor': return new THREE.Color(0.82, 0.5, 1);
    case 'supply': return new THREE.Color(0.5, 1, 0.55);
    case 'tech': return new THREE.Color(0.7, 0.94, 1);
    case 'research': return new THREE.Color(1, 0.76, 0.34);
    case 'academy': return unitAccent('caster');
    default: return new THREE.Color(1, 0.9, 0.77);
  }
}

function unitShapes(role: string): { shapes: Shape[]; height: number } {
  const p = (...part: Part): Shape => ({ kind: 'part', part });
  const s: Shape[] = [];
  let height: number;
  switch (role) {
    case 'worker':
      s.push(p(0, 0.3, -0.04, 0.56, 0.4, 0.52), p(0, 0.62, -0.07, 0.44, 0.3, 0.38, 2), p(-0.2, 0.16, 0.25, 0.1, 0.14, 0.26, 1), p(0.2, 0.16, 0.25, 0.1, 0.14, 0.26, 1));
      height = 0.85; break;
    case 'scout':
      s.push(p(0, 0.25, 0, 0.26, 0.28, 0.62), p(0, 0.19, -0.08, 0.7, 0.09, 0.26, 2), p(0, 0.48, -0.18, 0.06, 0.3, 0.06, 1), p(0, 0.65, -0.18, 0.16, 0.12, 0.16, 2));
      height = 0.78; break;
    case 'melee':
      s.push(p(-0.15, 0.24, 0, 0.18, 0.48, 0.25, 1), p(0.15, 0.24, 0, 0.18, 0.48, 0.25, 1), p(0, 0.82, 0, 0.5, 0.66, 0.34), p(0, 1.04, 0, 0.7, 0.22, 0.28),
        p(0, 1.32, 0, 0.26, 0.26, 0.26), p(0, 0.8, 0.24, 0.44, 0.6, 0.12, 2));
      height = 1.5; break;
    case 'ranged':
      s.push(p(-0.12, 0.22, 0, 0.14, 0.44, 0.2, 1), p(0.12, 0.22, 0, 0.14, 0.44, 0.2, 1), p(0, 0.7, 0, 0.32, 0.56, 0.28), p(0, 1.12, -0.04, 0.25, 0.26, 0.25),
        p(0.17, 0.74, 0.27, 0.13, 0.15, 0.8, 1), p(0.17, 0.74, 0.65, 0.16, 0.17, 0.14, 2));
      height = 1.32; break;
    case 'caster':
      s.push(p(0, 0.42, -0.08, 0.32, 0.48, 0.3, 1), p(-0.2, 0.52, -0.03, 0.18, 0.62, 0.37), p(0.2, 0.52, -0.03, 0.18, 0.62, 0.37), p(0, 0.87, -0.06, 0.36, 0.34, 0.3),
        p(0, 1.14, -0.04, 0.22, 0.22, 0.24, 1), p(0, 1.16, 0.09, 0.16, 0.08, 0.04, 2));
      for (const side of [-1, 1]) s.push(p(side * 0.28, 1.02, -0.04, 0.13, 0.25, 0.25), p(side * 0.28, 1.3, -0.04, 0.075, 0.33, 0.1, 2), p(side * 0.2, 0.82, 0.2, 0.12, 0.12, 0.38), p(side * 0.2, 0.82, 0.4, 0.14, 0.13, 0.12, 1));
      s.push({ kind: 'lobe', part: [0, 0.82, 0.45, 0.27, 0.27, 0.27, 2] });
      height = 1.5; break;
    default:
      s.push(p(0, 0.22, -0.04, 0.64, 0.3, 0.58), p(-0.28, 0.15, 0, 0.14, 0.24, 0.65, 1), p(0.28, 0.15, 0, 0.14, 0.24, 0.65, 1), p(0, 0.5, -0.06, 0.46, 0.3, 0.36),
        p(-0.15, 0.55, 0.3, 0.12, 0.15, 0.88, 1), p(0.15, 0.55, 0.3, 0.12, 0.15, 0.88, 1), p(-0.15, 0.55, 0.7, 0.17, 0.18, 0.14, 2), p(0.15, 0.55, 0.7, 0.17, 0.18, 0.14, 2));
      height = 0.88; break;
  }
  return { shapes: s, height };
}

function buildingShapes(role: string, bio: boolean): { shapes: Shape[]; height: number } {
  const p = (...part: Part): Shape => ({ kind: 'part', part });
  const box = (...part: Part): Shape => ({ kind: 'part', part });
  const prism = (sides: number, ...part: Part): Shape => ({ kind: 'prism', part, sides });
  const tube = (x: number, y: number, z: number, outer: number, inner: number, height: number, material: number): Shape => ({ kind: 'tube', center: [x, y, z], outer, inner, height, material });
  const lobe = (...part: Part): Shape => ({ kind: 'lobe', part });
  const s: Shape[] = [bio ? lobe(0, 0.08, 0, 0.96, 0.16, 0.96, 1) : prism(8, 0, 0.05, 0, 0.96, 0.1, 0.96, 1)];
  let height: number;
  switch (role) {
    case 'hq':
      s.push(p(0, 0.19, 0, 0.82, 0.25, 0.82), p(0, 0.41, 0.12, 0.52, 0.38, 0.5), p(0, 0.66, 0.12, 0.3, 0.2, 0.3, 1));
      for (let i = -1; i <= 1; i++) s.push(p(i * 0.13, i === 0 ? 0.8 : 0.75, 0.12, 0.085, i === 0 ? 0.2 : 0.12, 0.12, 2));
      for (const x of [-1, 1]) for (const z of [-1, 1]) s.push(p(x * 0.32, 0.25, z * 0.32, 0.18, 0.3, 0.18));
      height = 0.9; break;
    case 'barracks':
      s.push(p(0, 0.22, 0.06, 0.84, 0.27, 0.72));
      if (bio) { for (let i = -1; i <= 1; i++) s.push(p(i * 0.24, 0.4, 0.16, 0.23, 0.35, 0.37, 2)); s.push(p(-0.32, 0.26, -0.25, 0.16, 0.36, 0.25), p(0.32, 0.26, -0.25, 0.16, 0.36, 0.25)); }
      else { s.push(box(0, 0.39, 0.12, 0.9, 0.13, 0.65, 0), box(0, 0.23, -0.308, 0.6, 0.27, 0.028, 1)); for (let i = -1; i <= 1; i++) s.push(box(i * 0.23, 0.47, 0.12, 0.09, 0.04, 0.5, 2)); }
      s.push(box(0, 0.12, -0.36, 0.58, 0.05, 0.22, 1));
      height = 0.6; break;
    case 'extractor':
      s.push(p(0, 0.2, 0, 0.77, 0.25, 0.72));
      for (const i of [-1, 1]) s.push(p(i * 0.23, 0.47, 0.12, 0.29, 0.63, 0.31), tube(i * 0.23, 0.76, 0.12, 0.16, 0.11, 0.1, 2));
      s.push(p(0, 0.24, -0.29, 0.58, 0.19, 0.22, 2));
      height = 0.85; break;
    case 'supply':
      for (let i = -1; i <= 1; i++) s.push(p(i * 0.27, 0.23, 0.04, 0.24, 0.29, 0.58), p(i * 0.27, 0.4, 0.13, 0.21, 0.08, 0.34, 2));
      height = 0.5; break;
    case 'tech':
      s.push(p(-0.29, 0.41, 0.11, 0.15, 0.65, 0.28), p(0.29, 0.41, 0.11, 0.15, 0.65, 0.28), lobe(0, 0.54, 0.11, 0.31, 0.83, 0.31, 2),
        tube(0, 0.42, 0.11, 0.29, 0.24, 0.07, 1), tube(0, 0.7, 0.11, 0.27, 0.22, 0.05, 0));
      height = 1; break;
    case 'research':
      s.push(box(0, 0.2, 0.08, 0.8, 0.22, 0.64, 0));
      for (const side of [-1, 1]) s.push(prism(6, side * 0.23, 0.4, 0.1, 0.31, 0.38, 0.4, 1), lobe(side * 0.23, 0.56, 0.1, 0.25, 0.2, 0.32, 2), box(side * 0.23, 0.68, 0.1, 0.29, 0.045, 0.36, 0));
      s.push(box(0, 0.39, 0.1, 0.12, 0.32, 0.44, 0));
      height = 0.75; break;
    case 'academy':
      s.push(prism(6, 0, 0.15, 0.08, 0.74, 0.18, 0.66, 0));
      for (const [x, z] of [[-0.3, -0.02], [0.3, -0.02], [0, 0.32]]) s.push(prism(6, x, 0.4, z, 0.17, 0.61, 0.17, 0), prism(6, x, 0.75, z, 0.11, 0.18, 0.11, 2));
      s.push(lobe(0, 0.49, 0.1, 0.35, 0.35, 0.35, 2), tube(0, 0.31, 0.1, 0.23, 0.18, 0.05, 1));
      height = 0.9; break;
    default:
      s.push(p(0, 0.37, 0.06, 0.38, 0.61, 0.38), p(0, 0.87, 0.06, 0.24, 0.51, 0.25), p(0, 1.16, 0.06, 0.55, 0.22, 0.45), p(0, 1.225, 0.24, 0.13, 0.13, 0.35, 1), p(0, 1.225, 0.41, 0.16, 0.16, 0.1, 2));
      height = 1.3; break;
  }
  return { shapes: s, height };
}

export type ModelState = 'normal' | 'construction' | 'memory' | 'ghost';

export interface ModelInfo { height: number; scale: number }

// Shared geometry and materials; every entity gets a light Group of meshes.
export class ModelLibrary {
  private readonly box = new THREE.BoxGeometry(1, 1, 1);
  private readonly sphere = new THREE.SphereGeometry(0.5, 14, 10);
  private readonly prisms = new Map<number, THREE.CylinderGeometry>();
  private readonly tubes = new Map<string, THREE.BufferGeometry>();
  private readonly materials = new Map<string, THREE.MeshStandardMaterial>();
  private readonly templates = new Map<string, { group: THREE.Group; info: ModelInfo }>();
  materialHook: ((material: THREE.MeshStandardMaterial) => void) | null = null;

  private prism(sides: number) {
    let geometry = this.prisms.get(sides);
    if (!geometry) { geometry = new THREE.CylinderGeometry(0.5, 0.5, 1, sides); this.prisms.set(sides, geometry); }
    return geometry;
  }
  private tube(outer: number, inner: number, height: number) {
    const key = `${outer}/${inner}/${height}`;
    let geometry = this.tubes.get(key);
    if (!geometry) {
      const shape = new THREE.Shape(); shape.absarc(0, 0, outer, 0, Math.PI * 2, false);
      const hole = new THREE.Path(); hole.absarc(0, 0, inner, 0, Math.PI * 2, true); shape.holes.push(hole);
      geometry = new THREE.ExtrudeGeometry(shape, { depth: height, bevelEnabled: false, curveSegments: 20 });
      geometry.rotateX(-Math.PI / 2); geometry.translate(0, -height / 2, 0);
      this.tubes.set(key, geometry);
    }
    return geometry;
  }

  material(slot: number, own: boolean, role: string, building: boolean, faction: string, state: ModelState): THREE.MeshStandardMaterial {
    const key = `${slot}/${own}/${role}/${building}/${faction}/${state}`;
    let material = this.materials.get(key);
    if (material) return material;
    const bio = faction === 'biological';
    let color = slot === 0 ? (own ? OwnColor : EnemyColor).clone() : slot === 1 ? StructureColor.clone() : (building ? buildingAccent(role) : unitAccent(role)).clone();
    if (bio && slot === 0) color.lerp(new THREE.Color(0.55, 0.3, 0.75), 0.18);
    if (state === 'construction') color = new THREE.Color().setScalar(slot === 0 ? 0.52 : slot === 1 ? 0.19 : 0.78);
    if (state === 'memory') { const l = color.getHSL({ h: 0, s: 0, l: 0 }).l; color.setHSL(0, 0, l * 0.8); }
    material = new THREE.MeshStandardMaterial({
      color, roughness: bio ? 0.75 : 0.55, metalness: bio ? 0.05 : 0.25,
      emissive: slot === 2 && state === 'normal' ? color.clone().multiplyScalar(0.35) : new THREE.Color(0),
      transparent: state === 'ghost', opacity: state === 'ghost' ? 0.55 : 1, depthWrite: state !== 'ghost',
    });
    this.materialHook?.(material);
    this.materials.set(key, material);
    return material;
  }

  info(type: Archetype): ModelInfo {
    const scale = type.building ? type.footprint : type.role === 'siege' ? 1.8 : 1;
    const height = type.building ? buildingShapes(type.role, type.faction === 'biological').height * type.footprint : unitShapes(type.role).height * scale;
    return { height, scale };
  }

  // A new Group per entity; geometry and materials are shared.
  create(type: Archetype, own: boolean, state: ModelState = 'normal'): THREE.Group {
    const key = `${type.id}/${own}/${state}`;
    let template = this.templates.get(key);
    if (!template) {
      template = { group: this.build(type, own, state), info: this.info(type) };
      this.templates.set(key, template);
    }
    const group = template.group.clone(false);
    for (const child of template.group.children) group.add((child as THREE.Mesh).clone(false));
    group.userData.info = template.info;
    return group;
  }

  private build(type: Archetype, own: boolean, state: ModelState): THREE.Group {
    const bio = type.faction === 'biological';
    const { shapes } = type.building ? buildingShapes(type.role, bio) : unitShapes(type.role);
    const scale = type.building ? type.footprint : type.role === 'siege' ? 1.8 : 1;
    const group = new THREE.Group();
    const mat = (slot: number) => this.material(slot, own, type.role, type.building, type.faction, state);
    // Unity model space (+z forward/north) → three.js world (map y is -z).
    const place = (mesh: THREE.Mesh, x: number, y: number, z: number, sx: number, sy: number, sz: number) => {
      mesh.position.set(x * scale, y * scale, -z * scale); mesh.scale.set(sx * scale, sy * scale, sz * scale);
      mesh.castShadow = state !== 'ghost'; mesh.receiveShadow = state !== 'ghost';
      group.add(mesh);
    };
    for (const shape of shapes) {
      if (shape.kind === 'tube') {
        const mesh = new THREE.Mesh(this.tube(shape.outer, shape.inner, shape.height), mat(shape.material));
        place(mesh, shape.center[0], shape.center[1], shape.center[2], 1, 1, 1);
        continue;
      }
      const [x, y, z, sx, sy, sz, slot = 0] = shape.part;
      const round = shape.kind === 'lobe' || (bio && shape.kind === 'part');
      const geometry = shape.kind === 'prism' ? this.prism(shape.sides) : round ? this.sphere : this.box;
      place(new THREE.Mesh(geometry, mat(slot)), x, y, z, sx, sy, sz);
    }
    return group;
  }

  dispose(): void {
    this.box.dispose(); this.sphere.dispose();
    for (const g of this.prisms.values()) g.dispose();
    for (const g of this.tubes.values()) g.dispose();
    for (const m of this.materials.values()) m.dispose();
    this.templates.clear(); this.materials.clear();
  }
}
