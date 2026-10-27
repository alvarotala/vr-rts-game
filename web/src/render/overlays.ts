import * as THREE from 'three';
import type { GameSession } from '../game/session';
import { CommandKind, type Point } from '../protocol/types';
import type { ModelLibrary } from './models';
import type { TerrainOverlays } from './terrainView';

const OrderColors: Partial<Record<CommandKind, number>> = {
  [CommandKind.Move]: 0x4df2ef, [CommandKind.AttackMove]: 0xff4a3d, [CommandKind.Attack]: 0xff4a3d, [CommandKind.Gather]: 0x63d6ff,
  [CommandKind.Repair]: 0x8cff8c, [CommandKind.Build]: 0xffc94d, [CommandKind.Cast]: 0xd966ff,
};

// Planning feedback: construction ghost and footprint validity field, rally flags,
// the order plan of selected units, command pulses and targeting rings.
export class OverlayLayer {
  readonly group = new THREE.Group();
  private ghost: THREE.Group | null = null;
  private ghostDefinition = '';
  private ghostValid: boolean | null = null;
  private readonly lines: THREE.LineSegments;
  private readonly lineGeometry = new THREE.BufferGeometry();
  private readonly dots: THREE.InstancedMesh;
  private readonly flags: THREE.InstancedMesh;
  private placementKey = '';
  pointer: Point | null = null;
  pointerResource = 0;

  constructor(private readonly session: GameSession, private readonly models: ModelLibrary, private readonly terrain: TerrainOverlays) {
    this.group.name = 'overlays';
    this.lines = new THREE.LineSegments(this.lineGeometry, new THREE.LineBasicMaterial({ vertexColors: true, transparent: true, opacity: 0.75, depthTest: false }));
    this.lines.renderOrder = 3; this.lines.frustumCulled = false;
    this.group.add(this.lines);
    this.dots = new THREE.InstancedMesh(new THREE.CircleGeometry(0.16, 12).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ depthTest: false, transparent: true, opacity: 0.9 }), 256);
    this.dots.renderOrder = 3; this.dots.frustumCulled = false; this.dots.count = 0;
    this.group.add(this.dots);
    const flag = new THREE.Group();
    this.flags = new THREE.InstancedMesh(new THREE.ConeGeometry(0.22, 0.9, 4).translate(0, 0.45, 0), new THREE.MeshBasicMaterial({ color: 0x3df0ff }), 32);
    this.flags.count = 0; this.flags.frustumCulled = false;
    this.group.add(this.flags);
    void flag;
  }

  private ground(x: number, y: number, lift = 0.12) { return new THREE.Vector3(x, this.session.terrain.heightAt(x, y) + lift, -y); }

  update(time: number): void {
    const session = this.session;
    this.updateGhost();
    this.updatePlacementField();
    // Command pulse (cyan for Move, red for attacks), 1.2 s like GameSession.CommandPulseDuration.
    const pulse = session.lastCommand;
    const age = pulse ? time - pulse.time : 99;
    if (pulse && age < 1.2 && session.canIssueOrders) {
      const radius = 0.65 + (1.6 - 0.65) * Math.min(1, age / 1.2);
      this.terrain.pulse.set(pulse.point.x, pulse.point.y, radius, 1 - smooth(Math.max(0, (age / 1.2 - 0.35) / 0.65)));
      this.terrain.pulseColor.setHex(pulse.kind === CommandKind.Move ? 0x4df2ef : 0xff3d2e);
    } else this.terrain.pulse.w = 0;
    // Ability area/range and Attack Move cursor.
    const ability = session.pendingAbility;
    this.terrain.area.w = 0; this.terrain.range.w = 0; this.terrain.cursor.w = 0;
    if (ability && this.pointer && session.view) {
      const p = this.pointer;
      const index = Math.floor(p.y) * session.map.width + Math.floor(p.x);
      const seen = session.view.visibility[index] === 2;
      this.terrain.areaColor.setHex(!seen ? 0xff3d2e : ability.effect === 'heal' ? 0x4dff8c : 0xd966ff);
      this.terrain.area.set(p.x, p.y, ability.radius, 1);
      const casters = session.abilityCasters(ability.id).filter((e) => session.abilityReady(e, ability.id))
        .sort((a, b) => Math.hypot(a.position.x - p.x, a.position.y - p.y) - Math.hypot(b.position.x - p.x, b.position.y - p.y));
      if (casters[0]) this.terrain.range.set(casters[0].position.x, casters[0].position.y, ability.range, 1);
    } else if (session.attackMoveMode && this.pointer) this.terrain.cursor.set(this.pointer.x, this.pointer.y, 0.55, 1);
    else if (session.rallyPointMode && this.pointer) { this.terrain.cursor.set(this.pointer.x, this.pointer.y, 0.55, 1); }
    this.terrain.cursorColor.setHex(session.attackMoveMode ? 0xff3d2e : 0x4df2ef);
    this.updatePlan();
  }

  private updateGhost() {
    const session = this.session, definition = session.buildDefinition;
    if (!definition || !this.pointer) { if (this.ghost) this.ghost.visible = false; return; }
    const valid = (() => {
      const origin = session.buildPlacement(this.pointer!, this.pointerResource);
      return { origin, ok: session.canPreviewBuild(origin, definition) };
    })();
    if (!this.ghost || this.ghostDefinition !== definition || this.ghostValid !== valid.ok) {
      if (this.ghost) this.group.remove(this.ghost);
      const type = session.catalog.get(definition);
      this.ghost = this.models.create(type, true, 'ghost');
      this.ghost.traverse((child) => {
        const mesh = child as THREE.Mesh;
        if (!mesh.isMesh) return;
        const material = (mesh.material as THREE.MeshStandardMaterial).clone();
        material.color.lerp(new THREE.Color(valid.ok ? 0x4dff8c : 0xff4a3d), 0.55); material.emissive.setHex(valid.ok ? 0x0f3a1c : 0x3a0f0f);
        mesh.material = material; mesh.castShadow = false;
      });
      this.ghostDefinition = definition; this.ghostValid = valid.ok;
      this.group.add(this.ghost);
    }
    const footprint = session.catalog.get(definition).footprint;
    const cx = valid.origin.x + footprint / 2, cy = valid.origin.y + footprint / 2;
    this.ghost.visible = true;
    this.ghost.position.set(cx, session.terrain.heightAt(cx, cy) + 0.02, -cy);
  }

  // Green where the whole footprint fits, red where it is blocked, fading around the cursor.
  private updatePlacementField() {
    const session = this.session, data = this.terrain.placementData, definition = session.buildDefinition;
    const width = session.map.width, height = session.map.height;
    if (!definition || !this.pointer || !session.view) {
      if (this.placementKey !== '') { data.fill(0); this.terrain.placement.needsUpdate = true; this.placementKey = ''; }
      return;
    }
    const origin = session.buildPlacement(this.pointer, this.pointerResource);
    const key = `${definition}/${origin.x}/${origin.y}/${session.view.tick}`;
    if (key === this.placementKey) return;
    this.placementKey = key;
    const mask = session.placementMask(definition);
    data.fill(0);
    const radius = 9;
    for (let y = Math.max(0, origin.y - radius); y <= Math.min(height - 1, origin.y + radius); y++)
      for (let x = Math.max(0, origin.x - radius); x <= Math.min(width - 1, origin.x + radius); x++) {
        const d = Math.hypot(x - origin.x, y - origin.y);
        if (d > radius) continue;
        const alpha = Math.round(255 * 0.42 * (1 - d / radius));
        const i = (y * width + x) * 4, ok = mask[y * width + x] === 1;
        data[i] = ok ? 70 : 240; data[i + 1] = ok ? 230 : 70; data[i + 2] = ok ? 110 : 60; data[i + 3] = alpha;
      }
    this.terrain.placement.needsUpdate = true;
  }

  // Active destination and queued steps of selected units; rally lines of selected producers.
  private updatePlan() {
    const session = this.session, view = session.view;
    const positions: number[] = [], colors: number[] = [];
    let dots = 0, flags = 0;
    const matrix = new THREE.Matrix4(), color = new THREE.Color();
    const addLine = (a: THREE.Vector3, b: THREE.Vector3, hex: number) => {
      color.setHex(hex); positions.push(a.x, a.y, a.z, b.x, b.y, b.z); colors.push(color.r, color.g, color.b, color.r, color.g, color.b);
    };
    const addDot = (p: THREE.Vector3, hex: number) => {
      if (dots >= 256) return;
      matrix.makeTranslation(p.x, p.y, p.z); this.dots.setMatrixAt(dots, matrix); this.dots.setColorAt(dots, color.setHex(hex)); dots++;
    };
    if (view) {
      const selected = session.selected().filter((e) => !e.remembered);
      let units = 0;
      for (const entity of selected) {
        const type = session.catalog.get(entity.definition);
        if (type.building) {
          if (entity.hasRallyPoint && flags < 32) {
            const center = session.catalog.center(entity.definition, entity.position);
            const target = this.ground(entity.rallyPoint.x, entity.rallyPoint.y, 0);
            addLine(this.ground(center.x, center.y, 0.3), target.clone().setY(target.y + 0.2), 0x3df0ff);
            matrix.makeTranslation(target.x, target.y, target.z); this.flags.setMatrixAt(flags++, matrix);
          }
          continue;
        }
        if (units++ >= 24) continue;
        const from = session.presentation.position(entity);
        let previous = this.ground(from.x, from.y);
        const moving = entity.order === CommandKind.Move || entity.order === CommandKind.AttackMove || entity.order === CommandKind.Attack;
        if (moving) {
          const target = entity.order === CommandKind.Attack ? view.entities.find((e) => e.id === entity.target) : undefined;
          const destination = target ? session.catalog.center(target.definition, target.position) : entity.destination;
          const next = this.ground(destination.x, destination.y);
          const hex = OrderColors[entity.order] ?? 0xffffff;
          addLine(previous, next, hex); addDot(next, hex); previous = next;
        }
        for (const order of entity.pendingOrders.slice(0, 8)) {
          const hex = OrderColors[order.kind] ?? 0xffffff;
          let point = order.position;
          if (order.kind === CommandKind.Gather) point = view.resources.find((r) => r.id === order.target)?.position ?? point;
          else if (order.kind === CommandKind.Attack || order.kind === CommandKind.Repair) {
            const target = view.entities.find((e) => e.id === order.target);
            if (target) point = session.catalog.center(target.definition, target.position);
          }
          const next = this.ground(point.x, point.y);
          addLine(previous, next, hex); addDot(next, hex); previous = next;
        }
      }
    }
    this.lineGeometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    this.lineGeometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
    this.lineGeometry.computeBoundingSphere();
    this.dots.count = dots; this.dots.instanceMatrix.needsUpdate = true; if (this.dots.instanceColor) this.dots.instanceColor.needsUpdate = true;
    this.flags.count = flags; this.flags.instanceMatrix.needsUpdate = true;
  }
}

const smooth = (x: number) => x * x * (3 - 2 * x);
