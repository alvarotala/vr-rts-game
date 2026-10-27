import * as THREE from 'three';
import type { GameSession } from '../game/session';
import { TickRate } from '../game/catalog';
import type { EntityLayer } from './entities';
import type { ModelLibrary } from './models';

interface Tracer { mesh: THREE.Mesh; from: THREE.Vector3; to: THREE.Vector3; start: number; duration: number; arc: number; impact: THREE.Mesh; impactTime: number; size: number }
interface Burst { mesh: THREE.Mesh; start: number; duration: number; radius: number }
interface Corpse { root: THREE.Group; start: number; materials: THREE.MeshStandardMaterial[]; tilt: THREE.Vector3 }

// Cosmetic effects driven by the events in PlayerView: shots (AttackEvent), abilities
// (AbilityEvent), deaths (DeathEvent) and destroyed structures. They never decide damage.
export class EffectLayer {
  readonly group = new THREE.Group();
  private readonly tracers: Tracer[] = [];
  private readonly bursts: Burst[] = [];
  private readonly corpses: Corpse[] = [];
  private readonly seenAttacks = new Set<number>();
  private readonly seenAbilities = new Set<number>();
  private readonly seenDeaths = new Set<number>();
  private readonly buildings = new Map<number, { x: number; y: number; footprint: number }>();
  private readonly projectile = new THREE.SphereGeometry(0.09, 8, 6);
  private readonly flash = new THREE.SphereGeometry(0.3, 10, 8);
  private readonly ring = new THREE.RingGeometry(0.9, 1, 48);
  private readonly materials = {
    own: new THREE.MeshBasicMaterial({ color: 0x7ef0ff }), enemy: new THREE.MeshBasicMaterial({ color: 0xff8a5c }),
    ownFlash: new THREE.MeshBasicMaterial({ color: 0xb8f6ff, transparent: true, depthWrite: false }),
    enemyFlash: new THREE.MeshBasicMaterial({ color: 0xffc29a, transparent: true, depthWrite: false }),
  };
  onImpact: ((own: boolean, heavy: boolean) => void) | null = null;
  onDeath: ((own: boolean, building: boolean) => void) | null = null;
  private time = 0;

  constructor(private readonly session: GameSession, private readonly entities: EntityLayer, private readonly models: ModelLibrary) {
    this.group.name = 'effects';
    this.ring.rotateX(-Math.PI / 2);
  }

  private height(x: number, y: number) { return this.session.terrain.heightAt(x, y); }

  update(time: number): void {
    this.time = time;
    const view = this.session.view;
    if (view) this.observe(time);
    for (let i = this.tracers.length - 1; i >= 0; i--) {
      const t = this.tracers[i];
      const age = time - t.start;
      if (age < t.duration) {
        const f = Math.max(0, age / t.duration);
        t.mesh.visible = true;
        t.mesh.position.lerpVectors(t.from, t.to, f);
        t.mesh.position.y += Math.sin(Math.PI * f) * t.arc;
        t.impact.visible = false;
      } else {
        t.mesh.visible = false;
        const impactAge = age - t.duration;
        if (impactAge === 0 || !t.impact.visible) { t.impact.visible = true; t.impact.position.copy(t.to); }
        const f = impactAge / t.impactTime;
        t.impact.scale.setScalar(t.size * (0.4 + f));
        (t.impact.material as THREE.MeshBasicMaterial).opacity = 1;
        t.impact.visible = f < 1;
        if (f >= 1) { this.group.remove(t.mesh); this.group.remove(t.impact); this.tracers.splice(i, 1); }
      }
    }
    for (let i = this.bursts.length - 1; i >= 0; i--) {
      const b = this.bursts[i];
      const f = (time - b.start) / b.duration;
      if (f >= 1) { this.group.remove(b.mesh); (b.mesh.material as THREE.Material).dispose(); this.bursts.splice(i, 1); continue; }
      b.mesh.scale.setScalar(Math.max(0.01, b.radius * (0.25 + 0.75 * Math.sin(f * Math.PI / 2))));
      (b.mesh.material as THREE.MeshBasicMaterial).opacity = 1 - f;
    }
    for (let i = this.corpses.length - 1; i >= 0; i--) {
      const c = this.corpses[i];
      const age = time - c.start;
      if (age > 6) { this.group.remove(c.root); for (const m of c.materials) m.dispose(); this.corpses.splice(i, 1); continue; }
      const fall = Math.min(1, age / 0.5);
      c.root.rotation.set(c.tilt.x * fall, c.root.rotation.y, c.tilt.z * fall);
      const fade = age < 3.5 ? 1 : 1 - (age - 3.5) / 2.5;
      for (const m of c.materials) m.opacity = fade;
      c.root.position.y = c.root.userData.ground - Math.max(0, age - 3.5) * 0.12;
    }
  }

  private observe(time: number) {
    const session = this.session, view = session.view!;
    const tick = session.presentation.animationTick;
    for (const attack of view.attacks) {
      if (this.seenAttacks.has(attack.id)) continue;
      this.seenAttacks.add(attack.id);
      if (tick - attack.tick > TickRate) continue;
      const type = session.catalog.find(attack.definition);
      const own = attack.owner === session.localPlayer;
      const siege = type?.role === 'siege';
      const melee = (type?.range ?? 2) <= 2;
      const fromHeight = (type?.building ? 1.2 : 0.75) * (siege ? 1.2 : 1);
      const from = new THREE.Vector3(attack.origin.x, this.height(attack.origin.x, attack.origin.y) + fromHeight, -attack.origin.y);
      const to = new THREE.Vector3(attack.destination.x, this.height(attack.destination.x, attack.destination.y) + 0.5, -attack.destination.y);
      const distance = from.distanceTo(to);
      const lag = Math.max(0, (tick - attack.tick) / TickRate);
      const mesh = new THREE.Mesh(this.projectile, own ? this.materials.own : this.materials.enemy);
      mesh.scale.setScalar(siege ? 2.2 : 1);
      const impact = new THREE.Mesh(this.flash, (own ? this.materials.ownFlash : this.materials.enemyFlash));
      impact.visible = false;
      this.group.add(mesh, impact);
      this.tracers.push({ mesh, from, to, start: time - lag, duration: melee ? 0.06 : Math.min(0.7, Math.max(0.08, distance / (siege ? 14 : 32))),
        arc: siege ? Math.min(3, distance * 0.25) : 0, impact, impactTime: siege ? 0.45 : 0.18, size: siege ? 2.4 : melee ? 0.8 : 1 });
      this.entities.attacking.set(attack.attacker, { x: attack.destination.x, y: attack.destination.y, until: time + 0.6 });
      this.onImpact?.(own, siege);
    }
    for (const event of view.abilityEvents) {
      if (this.seenAbilities.has(event.id)) continue;
      this.seenAbilities.add(event.id);
      if (tick - event.tick > TickRate) continue;
      const ability = session.catalog.ability(event.ability);
      const material = new THREE.MeshBasicMaterial({ color: ability?.effect === 'heal' ? 0x4dff8c : 0xd966ff, transparent: true, depthWrite: false, side: THREE.DoubleSide });
      const mesh = new THREE.Mesh(this.ring, material);
      mesh.position.set(event.position.x, this.height(event.position.x, event.position.y) + 0.15, -event.position.y);
      this.group.add(mesh);
      this.bursts.push({ mesh, start: time, duration: ability?.effect === 'heal' ? 0.98 : 0.8, radius: ability?.radius ?? 3 });
      const disc = new THREE.Mesh(new THREE.CircleGeometry(1, 40).rotateX(-Math.PI / 2), material.clone());
      disc.position.copy(mesh.position); disc.position.y += 0.01;
      (disc.material as THREE.MeshBasicMaterial).opacity = 0.35;
      this.group.add(disc);
      this.bursts.push({ mesh: disc, start: time, duration: 0.6, radius: (ability?.radius ?? 3) * 0.9 });
    }
    for (const death of view.deaths) {
      if (this.seenDeaths.has(death.unit)) continue;
      this.seenDeaths.add(death.unit);
      if (tick - death.tick > TickRate * 2) continue;
      this.spawnCorpse(death.definition, death.owner === session.localPlayer, death.position.x, death.position.y, death.direction.x, death.direction.y, time);
      this.onDeath?.(death.owner === session.localPlayer, false);
    }
    // Structures produce no DeathEvent: a visible building that vanishes was destroyed or cancelled.
    const present = new Set<number>();
    for (const entity of view.entities) {
      if (entity.remembered) continue;
      const type = session.catalog.find(entity.definition);
      if (!type?.building) continue;
      present.add(entity.id);
      this.buildings.set(entity.id, { x: entity.position.x + type.footprint / 2, y: entity.position.y + type.footprint / 2, footprint: type.footprint });
    }
    for (const [id, building] of this.buildings) {
      if (present.has(id)) continue;
      this.buildings.delete(id);
      if (view.visibility[Math.floor(building.y) * session.map.width + Math.floor(building.x)] !== 2) continue;
      const material = new THREE.MeshBasicMaterial({ color: 0xffb066, transparent: true, depthWrite: false });
      const mesh = new THREE.Mesh(this.flash, material);
      mesh.position.set(building.x, this.height(building.x, building.y) + 0.6, -building.y);
      this.group.add(mesh);
      this.bursts.push({ mesh, start: time, duration: 0.9, radius: building.footprint * 2.2 });
      this.onDeath?.(false, true);
    }
    if (this.seenAttacks.size > 4096) this.seenAttacks.clear();
    if (this.seenAbilities.size > 1024) this.seenAbilities.clear();
    if (this.seenDeaths.size > 4096) this.seenDeaths.clear();
  }

  private spawnCorpse(definition: string, own: boolean, x: number, y: number, dx: number, dy: number, time: number) {
    const type = this.session.catalog.find(definition);
    if (!type || this.corpses.length >= 64) return;
    const root = this.models.create(type, own, 'memory');
    const materials: THREE.MeshStandardMaterial[] = [];
    root.traverse((child) => {
      const mesh = child as THREE.Mesh;
      if (!mesh.isMesh) return;
      const material = (mesh.material as THREE.MeshStandardMaterial).clone();
      material.transparent = true; material.color.multiplyScalar(0.55); materials.push(material); mesh.material = material;
    });
    const ground = this.height(x, y);
    root.position.set(x, ground, -y);
    root.userData.ground = ground;
    const yaw = Math.hypot(dx, dy) > 0.001 ? Math.atan2(-dx, dy) : Math.random() * Math.PI * 2;
    root.rotation.y = yaw;
    this.group.add(root);
    this.corpses.push({ root, start: time, materials, tilt: new THREE.Vector3((Math.random() - 0.5) * 0.4, 0, (Math.random() > 0.5 ? 1 : -1) * 1.2) });
  }

  clear(): void {
    for (const t of this.tracers) this.group.remove(t.mesh, t.impact);
    for (const b of this.bursts) this.group.remove(b.mesh);
    for (const c of this.corpses) this.group.remove(c.root);
    this.tracers.length = 0; this.bursts.length = 0; this.corpses.length = 0;
    void this.time;
  }
}
