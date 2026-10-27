import * as THREE from 'three';
import type { GameSession } from '../game/session';
import type { Point } from '../protocol/types';
import { FogOfWar } from './fog';
import { ModelLibrary } from './models';
import { buildTerrain, palette, type TerrainOverlays } from './terrainView';
import { buildScenery } from './scenery';
import { ResourceLayer } from './resources';
import { EntityLayer } from './entities';
import { EffectLayer } from './effects';
import { OverlayLayer } from './overlays';
import { RtsCamera } from './camera';
import { rayToMap, rayToPlane } from './picking';

export interface PointerTarget { point: Point | null; plane: Point | null; entity: number; resource: number }

// Owns the three.js scene for one match and renders the session's latest view.
export class BattleView {
  readonly renderer: THREE.WebGLRenderer;
  readonly scene = new THREE.Scene();
  readonly rts: RtsCamera;
  readonly fog: FogOfWar;
  readonly models = new ModelLibrary();
  readonly entities: EntityLayer;
  readonly effects: EffectLayer;
  readonly overlays: OverlayLayer;
  readonly resources: ResourceLayer;
  private readonly overlaysTerrain: TerrainOverlays;
  private readonly sun: THREE.DirectionalLight;
  private readonly raycaster = new THREE.Raycaster();
  private readonly ndc = new THREE.Vector2();
  private time = 0;
  shadows = true;

  constructor(readonly canvas: HTMLCanvasElement, readonly session: GameSession) {
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFShadowMap;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    const theme = session.map.theme;
    const colors = palette(theme);
    this.scene.background = colors.background;
    this.scene.fog = new THREE.Fog(colors.background, 160, 420);
    this.fog = new FogOfWar(session.map.width, session.map.height);
    this.fog.color.copy(colors.fog);
    const terrain = buildTerrain(session.terrain, this.fog, theme);
    this.overlaysTerrain = terrain.overlays;
    this.scene.add(terrain.mesh, terrain.skirt, buildScenery(session.terrain, this.fog, theme));
    const light = theme === 'desert' ? 0xfff0dc : theme === 'forest' ? 0xf8ffef : theme === 'neon' ? 0xe0ecff : 0xffffff;
    this.scene.add(new THREE.HemisphereLight(0xdfefff, 0x2a2f2a, theme === 'neon' ? 1.1 : 1.35));
    this.sun = new THREE.DirectionalLight(light, theme === 'neon' ? 1.6 : 2.2);
    this.sun.castShadow = true;
    this.sun.shadow.mapSize.set(2048, 2048);
    const s = this.sun.shadow.camera as THREE.OrthographicCamera;
    s.left = -45; s.right = 45; s.top = 45; s.bottom = -45; s.near = 1; s.far = 160;
    this.sun.shadow.bias = -0.0008; this.sun.shadow.normalBias = 0.02;
    this.scene.add(this.sun, this.sun.target);
    this.resources = new ResourceLayer(session.terrain, this.fog);
    this.entities = new EntityLayer(session, this.models, this.fog);
    this.effects = new EffectLayer(session, this.entities, this.models);
    this.overlays = new OverlayLayer(session, this.models, this.overlaysTerrain);
    this.scene.add(this.resources.group, this.entities.group, this.effects.group, this.overlays.group);
    this.rts = new RtsCamera(session.terrain, 1);
    this.resize();
  }

  resize(): void {
    const width = this.canvas.clientWidth || window.innerWidth, height = this.canvas.clientHeight || window.innerHeight;
    this.renderer.setSize(width, height, false);
    this.rts.resize(width / Math.max(1, height));
  }

  frame(dt: number): void {
    this.time += dt;
    const session = this.session, view = session.view;
    if (view) this.fog.update(view.visibility, view.tick);
    this.rts.update(dt);
    const t = this.rts.target;
    const center = new THREE.Vector3(t.x, 0, -t.y);
    this.sun.position.set(center.x - 30, 60, center.z + 35);
    this.sun.target.position.copy(center);
    this.sun.castShadow = this.shadows;
    if (view) {
      const hiddenVapor = new Set(view.resources.filter((r) => r.extractorId > 0 || view.entities.some((e) => {
        const type = session.catalog.find(e.definition);
        return type?.role === 'extractor' && r.position.x >= e.position.x && r.position.x < e.position.x + type.footprint && r.position.y >= e.position.y && r.position.y < e.position.y + type.footprint;
      })).map((r) => r.id));
      this.resources.update(view.resources, this.time, Math.floor(this.time * 1000), hiddenVapor);
    }
    this.entities.update(dt, this.time);
    this.effects.update(this.time);
    this.overlays.update(performance.now() / 1000);
    this.renderer.render(this.scene, this.rts.camera);
  }

  ray(clientX: number, clientY: number): THREE.Ray {
    const rect = this.canvas.getBoundingClientRect();
    this.ndc.set(((clientX - rect.left) / rect.width) * 2 - 1, -((clientY - rect.top) / rect.height) * 2 + 1);
    this.raycaster.setFromCamera(this.ndc, this.rts.camera);
    return this.raycaster.ray;
  }

  // One pointer resolution per frame shared by hover, selection and orders.
  resolve(clientX: number, clientY: number): PointerTarget {
    const ray = this.ray(clientX, clientY).clone();
    const hit = rayToMap(ray, this.session.terrain);
    const point = hit?.point ?? null;
    const plane = point ?? rayToPlane(ray);
    const view = this.session.view;
    let entity = 0, resource = 0;
    const picked = this.entities.pick(ray);
    if (picked) entity = picked.id;
    if (view && point) {
      // Ground assistance for small units and deposits.
      if (!entity) {
        let best = 0.75;
        for (const e of view.entities) {
          if (e.remembered) continue;
          const type = this.session.catalog.find(e.definition);
          if (!type || type.building) continue;
          const p = this.session.presentation.position(e);
          const d = Math.hypot(p.x - point.x, p.y - point.y);
          if (d < best) { best = d; entity = e.id; }
        }
      }
      let best = 1.3;
      for (const r of view.resources) {
        if (r.remaining === 0) continue;
        const d = Math.hypot(r.position.x - point.x, r.position.y - point.y);
        if (d < best) { best = d; resource = r.id; }
      }
      if (resource && entity) {
        // A unit or building directly under the cursor wins over the deposit beside it.
        const e = view.entities.find((x) => x.id === entity);
        const type = e ? this.session.catalog.find(e.definition) : undefined;
        if (type && !type.building) resource = 0;
        else if (type?.role === 'extractor') resource = 0;
      }
    }
    return { point, plane, entity, resource };
  }

  // Screen projection of a map point (for drag rectangles and labels).
  project(point: Point): { x: number; y: number } {
    const v = new THREE.Vector3(point.x, this.session.terrain.heightAt(Math.max(0, Math.min(this.session.map.width - 0.01, point.x)), Math.max(0, Math.min(this.session.map.height - 0.01, point.y))), -point.y).project(this.rts.camera);
    const rect = this.canvas.getBoundingClientRect();
    return { x: rect.left + (v.x + 1) / 2 * rect.width, y: rect.top + (1 - v.y) / 2 * rect.height };
  }

  // Ground quadrilateral seen by the camera, in map coordinates (minimap frame).
  viewFrame(): Point[] {
    const rect = this.canvas.getBoundingClientRect();
    const corners: [number, number][] = [[rect.left, rect.top], [rect.right, rect.top], [rect.right, rect.bottom], [rect.left, rect.bottom]];
    return corners.map(([x, y]) => rayToPlane(this.ray(x, y).clone()) ?? { x: this.rts.target.x, y: this.rts.target.y });
  }

  dispose(): void {
    this.scene.traverse((object) => {
      const mesh = object as THREE.Mesh;
      if (mesh.geometry) mesh.geometry.dispose();
      const material = mesh.material as THREE.Material | THREE.Material[] | undefined;
      if (Array.isArray(material)) material.forEach((m) => m.dispose()); else material?.dispose();
    });
    this.models.dispose();
    this.fog.dispose();
    this.renderer.dispose();
  }
}
