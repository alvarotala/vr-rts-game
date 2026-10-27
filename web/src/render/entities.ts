import * as THREE from 'three';
import type { GameSession } from '../game/session';
import type { VisibleEntity } from '../protocol/types';
import { CommandKind } from '../protocol/types';
import { ModelLibrary, type ModelState } from './models';
import { applyFog, type FogOfWar } from './fog';

interface EntityVisual {
  id: number; definition: string; own: boolean; state: ModelState;
  root: THREE.Group; model: THREE.Group; height: number; radius: number; building: boolean; footprint: number;
  ring: THREE.Mesh; bar: THREE.Group; barFill: THREE.Sprite; barBack: THREE.Sprite; progressFill: THREE.Sprite; progressBack: THREE.Sprite;
  energyFill: THREE.Sprite; cargo: THREE.Mesh; yaw: number; last: THREE.Vector2; travelled: number; seen: number;
}

const ringGeometry = new THREE.RingGeometry(0.86, 1, 40);
ringGeometry.rotateX(-Math.PI / 2);
const squareRingGeometry = (() => {
  const shape = new THREE.Shape([new THREE.Vector2(-1, -1), new THREE.Vector2(1, -1), new THREE.Vector2(1, 1), new THREE.Vector2(-1, 1)]);
  const hole = new THREE.Path([new THREE.Vector2(-0.93, -0.93), new THREE.Vector2(-0.93, 0.93), new THREE.Vector2(0.93, 0.93), new THREE.Vector2(0.93, -0.93)]);
  shape.holes.push(hole);
  const geometry = new THREE.ShapeGeometry(shape); geometry.rotateX(-Math.PI / 2); return geometry;
})();
const RingColors = { selected: new THREE.Color(0.35, 1, 0.45), hoverOwn: new THREE.Color(1, 0.92, 0.32), enemy: new THREE.Color(1, 0.3, 0.18), resource: new THREE.Color(0.45, 0.85, 1) };

const spriteMaterial = (color: number, opacity = 1) => new THREE.SpriteMaterial({ color, transparent: true, opacity, depthWrite: false, depthTest: false, sizeAttenuation: true });

// Units, buildings and remembered buildings from the latest PlayerView, at interpolated
// positions on the terrain surface, with selection rings and health/progress bars.
export class EntityLayer {
  readonly group = new THREE.Group();
  private readonly visuals = new Map<number, EntityVisual>();
  private readonly ringMaterials = new Map<string, THREE.MeshBasicMaterial>();
  private readonly bars = { back: spriteMaterial(0x0b0f14, 0.85), high: spriteMaterial(0x48e06a), mid: spriteMaterial(0xf2c94c), low: spriteMaterial(0xf05545),
    progress: spriteMaterial(0xe8edf2), energy: spriteMaterial(0xb06bff) };
  private readonly cargoGeometry = new THREE.OctahedronGeometry(0.12);
  private readonly cargoMaterials = { mineral: new THREE.MeshBasicMaterial({ color: 0x53c8ff }), catalyst: new THREE.MeshBasicMaterial({ color: 0xc084fc }) };
  private frame = 0;
  hovered = 0;
  hoveredResource = 0;
  showAllBars = false;
  readonly attacking = new Map<number, { x: number; y: number; until: number }>();

  constructor(private readonly session: GameSession, readonly models: ModelLibrary, fog: FogOfWar) {
    this.group.name = 'entities';
    models.materialHook = (material) => applyFog(material, fog);
  }

  private ringMaterial(color: THREE.Color, opacity: number) {
    const key = color.getHexString() + opacity;
    let material = this.ringMaterials.get(key);
    if (!material) { material = new THREE.MeshBasicMaterial({ color, transparent: true, opacity, depthWrite: false, side: THREE.DoubleSide }); this.ringMaterials.set(key, material); }
    return material;
  }

  private stateOf(entity: VisibleEntity): ModelState { return entity.remembered ? 'memory' : entity.complete ? 'normal' : 'construction'; }

  private create(entity: VisibleEntity): EntityVisual {
    const type = this.session.catalog.get(entity.definition);
    const own = entity.owner === this.session.localPlayer;
    const state = this.stateOf(entity);
    const model = this.models.create(type, own, state);
    const info = this.models.info(type);
    const root = new THREE.Group();
    root.add(model);
    const ring = new THREE.Mesh(type.building ? squareRingGeometry : ringGeometry, this.ringMaterial(RingColors.selected, 0.9));
    const radius = type.building ? type.footprint * 0.5 : (type.collisionRadius + 0.14) * (type.role === 'siege' ? 1.5 : 1);
    ring.scale.setScalar(radius); ring.position.y = 0.06; ring.visible = false; ring.renderOrder = 2;
    root.add(ring);
    const bar = new THREE.Group();
    const sprite = (material: THREE.SpriteMaterial, height: number, order: number) => { const s = new THREE.Sprite(material); s.center.set(0, 0.5); s.scale.set(1, height, 1); s.renderOrder = order; bar.add(s); return s; };
    const width = type.building ? Math.min(3, type.footprint * 0.8) : 0.9;
    const barBack = sprite(this.bars.back, 0.16, 20), barFill = sprite(this.bars.high, 0.1, 21);
    const progressBack = sprite(this.bars.back, 0.12, 20), progressFill = sprite(this.bars.progress, 0.07, 21);
    const energyFill = sprite(this.bars.energy, 0.06, 22);
    barBack.scale.x = width + 0.06; barBack.position.x = -(width + 0.06) / 2; barFill.position.x = -width / 2;
    progressBack.scale.x = width + 0.06; progressBack.position.set(-(width + 0.06) / 2, -0.17, 0); progressFill.position.set(-width / 2, -0.17, 0);
    energyFill.position.set(-width / 2, -0.12, 0);
    bar.userData.width = width;
    bar.position.y = info.height + 0.35;
    bar.visible = false;
    root.add(bar);
    const cargo = new THREE.Mesh(this.cargoGeometry, this.cargoMaterials.mineral);
    cargo.position.set(0, 0.95, 0); cargo.visible = false;
    root.add(cargo);
    root.userData.entityId = entity.id;
    this.group.add(root);
    return { id: entity.id, definition: entity.definition, own, state, root, model, height: info.height, radius, building: type.building, footprint: type.footprint,
      ring, bar, barFill, barBack, progressFill, progressBack, energyFill, cargo, yaw: own ? 0 : Math.PI, last: new THREE.Vector2(entity.position.x, entity.position.y), travelled: 0, seen: 0 };
  }

  update(dt: number, time: number): void {
    const session = this.session, view = session.view;
    this.frame++;
    if (!view) { for (const visual of this.visuals.values()) visual.root.visible = false; return; }
    const terrain = session.terrain;
    const now = session.presentation.animationTick;
    for (const entity of view.entities) {
      let visual = this.visuals.get(entity.id);
      const state = this.stateOf(entity);
      if (visual && (visual.state !== state || visual.definition !== entity.definition)) {
        visual.root.remove(visual.model);
        const type = session.catalog.get(entity.definition);
        visual.model = this.models.create(type, visual.own, state); visual.state = state; visual.definition = entity.definition;
        visual.root.add(visual.model);
      }
      if (!visual) { visual = this.create(entity); this.visuals.set(entity.id, visual); }
      visual.seen = this.frame;
      visual.root.visible = true;
      const type = session.catalog.get(entity.definition);
      const position = type.building ? entity.position : session.presentation.position(entity);
      const cx = type.building ? position.x + type.footprint / 2 : position.x, cy = type.building ? position.y + type.footprint / 2 : position.y;
      const ground = terrain.heightAt(cx, cy);
      if (!type.building) {
        const dx = position.x - visual.last.x, dy = position.y - visual.last.y;
        const moved = Math.hypot(dx, dy);
        let desired: number | null = moved > 0.003 ? Math.atan2(-dx, dy) : null;
        const attack = this.attacking.get(entity.id);
        if (attack && attack.until > time && (moved < 0.02 || entity.order === CommandKind.Attack || entity.order === CommandKind.Hold || entity.order === CommandKind.Stop))
          desired = Math.atan2(-(attack.x - position.x), attack.y - position.y);
        if (desired !== null) {
          let delta = desired - visual.yaw;
          delta = Math.atan2(Math.sin(delta), Math.cos(delta));
          visual.yaw += delta * Math.min(1, dt * 12);
        }
        visual.travelled += moved;
        visual.last.set(position.x, position.y);
        const bob = moved > 0.003 ? Math.abs(Math.sin(visual.travelled * 9)) * 0.05 : 0;
        visual.model.position.y = bob;
        visual.model.rotation.y = visual.yaw;
        const hover = type.role === 'caster' ? 0.06 + Math.sin(time * 2 + entity.id) * 0.04 : 0;
        visual.model.position.y += hover;
      }
      visual.root.position.set(cx, ground, -cy);
      // Construction grows from its foundation while progress advances.
      if (!entity.complete && !entity.remembered) visual.model.scale.set(1, 0.25 + 0.75 * Math.max(0, Math.min(1, entity.progress)), 1);
      else visual.model.scale.set(1, 1, 1);
      // Selection, hover and inspection rings.
      const selected = session.selection.has(entity.id) || session.inspected === entity.id;
      const hovered = this.hovered === entity.id;
      const own = entity.owner === session.localPlayer;
      visual.ring.visible = (selected || hovered) && !entity.remembered;
      if (visual.ring.visible) visual.ring.material = this.ringMaterial(!own ? RingColors.enemy : selected ? RingColors.selected : RingColors.hoverOwn, selected ? 0.95 : 0.6);
      // Bars: selected, hovered, damaged or Alt held.
      const max = type.health;
      const damaged = entity.health < max - 0.01;
      const showBar = !entity.remembered && (selected || hovered || damaged || this.showAllBars || !entity.complete);
      visual.bar.visible = showBar;
      if (showBar) {
        const width = visual.bar.userData.width as number;
        const fraction = Math.max(0, Math.min(1, entity.health / max));
        visual.barFill.scale.x = Math.max(0.001, width * fraction);
        visual.barFill.material = fraction > 0.6 ? this.bars.high : fraction > 0.3 ? this.bars.mid : this.bars.low;
        const building = !entity.complete || (own && entity.queue.length > 0);
        let progress = !entity.complete ? entity.progress : 0;
        if (own && entity.complete && entity.queue.length > 0) { const order = entity.queue[0]; progress = order.total > 0 ? 1 - order.remaining / order.total : 0; }
        visual.progressBack.visible = visual.progressFill.visible = building;
        visual.progressFill.scale.x = Math.max(0.001, width * Math.max(0, Math.min(1, progress)));
        visual.energyFill.visible = own && type.energyMax > 0 && !building;
        if (visual.energyFill.visible) visual.energyFill.scale.x = Math.max(0.001, width * Math.min(1, entity.energy / type.energyMax));
      }
      visual.cargo.visible = own && entity.cargo > 0 && !type.building;
      if (visual.cargo.visible) { visual.cargo.material = entity.cargoKind === 'catalyst' ? this.cargoMaterials.catalyst : this.cargoMaterials.mineral; visual.cargo.rotation.y = time * 2; }
      void now;
    }
    for (const [id, visual] of this.visuals) if (visual.seen !== this.frame) { this.group.remove(visual.root); this.visuals.delete(id); }
  }

  // Ray hit test against model volumes (in front first), like BoardView.ResolvePointerTarget.
  pick(ray: THREE.Ray, exclude?: (id: number) => boolean): { id: number; distance: number } | null {
    let best: { id: number; distance: number } | null = null;
    const box = new THREE.Box3(), sphere = new THREE.Sphere(), hit = new THREE.Vector3();
    for (const visual of this.visuals.values()) {
      if (!visual.root.visible || (exclude && exclude(visual.id))) continue;
      const p = visual.root.position;
      let distance: number | null = null;
      if (visual.building) {
        const half = visual.footprint * 0.5;
        box.min.set(p.x - half, p.y, p.z - half); box.max.set(p.x + half, p.y + Math.max(0.6, visual.height), p.z + half);
        if (ray.intersectBox(box, hit)) distance = hit.distanceTo(ray.origin);
      } else {
        sphere.center.set(p.x, p.y + visual.height * 0.5, p.z); sphere.radius = Math.max(0.45, visual.radius * 1.1, visual.height * 0.5);
        if (ray.intersectSphere(sphere, hit)) distance = hit.distanceTo(ray.origin);
      }
      if (distance !== null && (!best || distance < best.distance)) best = { id: visual.id, distance };
    }
    return best;
  }

  worldPosition(id: number): THREE.Vector3 | null { const v = this.visuals.get(id); return v ? v.root.position.clone().setY(v.root.position.y + v.height * 0.5) : null; }
  modelOf(id: number): { model: THREE.Group; definition: string; own: boolean } | null { const v = this.visuals.get(id); return v ? { model: v.model, definition: v.definition, own: v.own } : null; }
  heightOf(id: number): number { return this.visuals.get(id)?.height ?? 1; }

  clear(): void { for (const visual of this.visuals.values()) this.group.remove(visual.root); this.visuals.clear(); }
}
