import * as THREE from 'three';
import type { Archetype } from '../game/catalog';
import { ModelLibrary } from '../render/models';

// Small portraits of the real models, rendered once per definition and team.
export class IconRenderer {
  private readonly renderer: THREE.WebGLRenderer | null;
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.PerspectiveCamera(30, 1, 0.1, 50);
  private readonly models = new ModelLibrary();
  private readonly cache = new Map<string, string>();
  constructor(size = 96) {
    let renderer: THREE.WebGLRenderer | null = null;
    try {
      const canvas = document.createElement('canvas');
      renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, preserveDrawingBuffer: true });
      renderer.setSize(size, size, false);
      renderer.outputColorSpace = THREE.SRGBColorSpace;
    } catch { renderer = null; }
    this.renderer = renderer;
    this.scene.add(new THREE.HemisphereLight(0xffffff, 0x334455, 2.2));
    const sun = new THREE.DirectionalLight(0xffffff, 2.4); sun.position.set(-2, 4, 3); this.scene.add(sun);
  }

  icon(type: Archetype, own = true): string {
    const key = type.id + '/' + own;
    const cached = this.cache.get(key);
    if (cached !== undefined) return cached;
    let url = '';
    if (this.renderer) {
      const model = this.models.create(type, own);
      const info = this.models.info(type);
      const box = new THREE.Box3().setFromObject(model);
      const center = box.getCenter(new THREE.Vector3());
      const radius = box.getSize(new THREE.Vector3()).length() * 0.5;
      model.rotation.y = type.building ? -0.6 : 0.6;
      this.scene.add(model);
      const distance = radius / Math.sin(THREE.MathUtils.degToRad(15)) * 0.95;
      this.camera.position.set(center.x + distance * 0.45, center.y + distance * 0.55, center.z + distance * 0.7);
      this.camera.lookAt(center.x, center.y - info.height * 0.05, center.z);
      this.renderer.render(this.scene, this.camera);
      url = this.renderer.domElement.toDataURL('image/png');
      this.scene.remove(model);
    }
    this.cache.set(key, url);
    return url;
  }

  dispose(): void { this.renderer?.dispose(); this.models.dispose(); }
}
