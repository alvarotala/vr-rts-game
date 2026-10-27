import * as THREE from 'three';
import type { Terrain } from '../game/terrain';

// Classic RTS camera: fixed yaw and pitch, pans across the map and zooms toward the ground.
export class RtsCamera {
  readonly camera: THREE.PerspectiveCamera;
  readonly target = new THREE.Vector2();
  distance = 34;
  private smoothDistance = 34;
  minDistance = 12;
  maxDistance = 95;
  private readonly pitch = THREE.MathUtils.degToRad(56);

  constructor(private readonly terrain: Terrain, aspect: number) {
    this.camera = new THREE.PerspectiveCamera(38, aspect, 0.5, 600);
    this.target.set(terrain.width / 2, terrain.height / 2);
    this.apply();
  }

  centerOn(x: number, y: number): void { this.target.set(x, y); this.clamp(); this.apply(); }
  pan(dx: number, dy: number): void { this.target.x += dx; this.target.y += dy; this.clamp(); }
  zoom(delta: number): void { this.distance = THREE.MathUtils.clamp(this.distance * Math.pow(1.0015, delta), this.minDistance, this.maxDistance); }
  fit(): void {
    this.target.set(this.terrain.width / 2, this.terrain.height / 2);
    this.distance = this.maxDistance = Math.max(95, Math.max(this.terrain.width, this.terrain.height) * 1.05);
  }
  get panSpeed(): number { return this.smoothDistance * 0.9; }

  private clamp() {
    this.target.x = THREE.MathUtils.clamp(this.target.x, 0, this.terrain.width);
    this.target.y = THREE.MathUtils.clamp(this.target.y, -6, this.terrain.height);
  }

  update(dt: number): void {
    this.smoothDistance += (this.distance - this.smoothDistance) * Math.min(1, dt * 12);
    this.apply();
  }

  private apply() {
    const ground = this.terrain.heightAt(THREE.MathUtils.clamp(this.target.x, 0, this.terrain.width - 0.01), THREE.MathUtils.clamp(this.target.y, 0, this.terrain.height - 0.01));
    const d = this.smoothDistance;
    this.camera.position.set(this.target.x, ground + d * Math.sin(this.pitch), -this.target.y + d * Math.cos(this.pitch));
    this.camera.lookAt(this.target.x, ground, -this.target.y);
    this.camera.updateMatrixWorld();
  }

  resize(aspect: number): void { this.camera.aspect = aspect; this.camera.updateProjectionMatrix(); }
}
