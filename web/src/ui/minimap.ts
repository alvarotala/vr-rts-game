import type { GameSession } from '../game/session';
import type { BattleView } from '../render/view';
import { palette } from '../render/terrainView';
import type { Point } from '../protocol/types';

// Overview of the board with fog, entities and the camera frame. Left button moves the
// camera; right button gives the contextual order at that point.
export class Minimap {
  readonly element: HTMLCanvasElement;
  private readonly context: CanvasRenderingContext2D;
  private readonly ground: HTMLCanvasElement;
  private readonly fogCanvas: HTMLCanvasElement;
  private readonly fogImage: ImageData;
  private readonly scale: number;
  private lastFogTick = -1;
  private dragging = false;
  private lastDraw = 0;
  onOrder: ((point: Point, append: boolean) => void) | null = null;

  constructor(private readonly session: GameSession, private readonly view: BattleView, size = 216) {
    const { width, height } = session.map;
    this.scale = size / Math.max(width, height);
    this.element = document.createElement('canvas');
    this.element.className = 'minimap';
    this.element.width = Math.round(width * this.scale); this.element.height = Math.round(height * this.scale);
    this.context = this.element.getContext('2d')!;
    this.ground = document.createElement('canvas'); this.ground.width = width; this.ground.height = height;
    const g = this.ground.getContext('2d')!;
    const image = g.createImageData(width, height);
    const colors = palette(session.map.theme);
    const terrain = session.terrain;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
      const i = terrain.index(x, y);
      const tint = !terrain.walkable[i] ? colors.blocked : terrain.ramp[i] ? colors.ramp : terrain.elevation[i] > 0 ? colors.high : { r: 1, g: 1, b: 1 };
      const o = ((height - 1 - y) * width + x) * 4;
      const boost = session.map.theme === 'neon' ? 2.2 : 1.15;
      image.data[o] = Math.min(255, colors.ground.r * tint.r * 255 * boost);
      image.data[o + 1] = Math.min(255, colors.ground.g * tint.g * 255 * boost);
      image.data[o + 2] = Math.min(255, colors.ground.b * tint.b * 255 * boost);
      image.data[o + 3] = 255;
    }
    g.putImageData(image, 0, 0);
    this.fogCanvas = document.createElement('canvas'); this.fogCanvas.width = width; this.fogCanvas.height = height;
    this.fogImage = this.fogCanvas.getContext('2d')!.createImageData(width, height);
    this.element.addEventListener('contextmenu', (e) => e.preventDefault());
    this.element.addEventListener('mousedown', (e) => {
      e.preventDefault(); e.stopPropagation();
      const point = this.toMap(e);
      if (e.button === 0) { this.dragging = true; this.view.rts.centerOn(point.x, point.y); }
      else if (e.button === 2) this.onOrder?.(point, e.shiftKey);
    });
    window.addEventListener('mousemove', this.move);
    window.addEventListener('mouseup', this.release);
  }

  private readonly move = (e: MouseEvent) => { if (this.dragging) { const p = this.toMap(e); this.view.rts.centerOn(p.x, p.y); } };
  private readonly release = () => { this.dragging = false; };

  private toMap(e: MouseEvent): Point {
    const rect = this.element.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width * this.session.map.width;
    const y = this.session.map.height - (e.clientY - rect.top) / rect.height * this.session.map.height;
    return { x: Math.max(0, Math.min(this.session.map.width - 0.01, x)), y: Math.max(0, Math.min(this.session.map.height - 0.01, y)) };
  }

  draw(now: number): void {
    if (now - this.lastDraw < 0.08) return;
    this.lastDraw = now;
    const session = this.session, view = session.view, c = this.context, s = this.scale;
    const { width, height } = session.map;
    c.imageSmoothingEnabled = true;
    c.drawImage(this.ground, 0, 0, this.element.width, this.element.height);
    if (view && view.tick !== this.lastFogTick && view.visibility.length === width * height) {
      this.lastFogTick = view.tick;
      const data = this.fogImage.data;
      for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        const v = view.visibility[y * width + x];
        const o = ((height - 1 - y) * width + x) * 4;
        data[o] = data[o + 1] = data[o + 2] = 0; data[o + 3] = v === 2 ? 0 : v === 1 ? 95 : 175;
      }
      this.fogCanvas.getContext('2d')!.putImageData(this.fogImage, 0, 0);
    }
    c.drawImage(this.fogCanvas, 0, 0, this.element.width, this.element.height);
    if (!view) return;
    for (const r of view.resources) {
      if (r.remaining === 0) continue;
      c.fillStyle = r.kind === 'catalyst' ? '#c084fc' : '#53c8ff';
      c.fillRect(r.position.x * s - 1.5, (height - r.position.y) * s - 1.5, 3, 3);
    }
    for (const e of view.entities) {
      const type = session.catalog.find(e.definition);
      if (!type) continue;
      const own = e.owner === view.player;
      c.fillStyle = e.remembered ? (own ? '#2a6a80' : '#7a3a30') : own ? (session.selection.has(e.id) ? '#c9ffd5' : '#33ccff') : '#ff5a3d';
      if (type.building) {
        const size = Math.max(3, type.footprint * s);
        c.fillRect(e.position.x * s, (height - e.position.y - type.footprint) * s, size, size);
      } else {
        const p = session.presentation.position(e);
        c.fillRect(p.x * s - 1.5, (height - p.y) * s - 1.5, 3, 3);
      }
    }
    const frame = this.view.viewFrame();
    c.strokeStyle = 'rgba(255,255,255,0.85)'; c.lineWidth = 1.2;
    c.beginPath();
    frame.forEach((p, i) => { const x = p.x * s, y = (height - p.y) * s; if (i === 0) c.moveTo(x, y); else c.lineTo(x, y); });
    c.closePath(); c.stroke();
  }

  dispose(): void { window.removeEventListener('mousemove', this.move); window.removeEventListener('mouseup', this.release); }
}
