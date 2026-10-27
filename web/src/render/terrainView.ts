import * as THREE from 'three';
import type { Terrain } from '../game/terrain';
import { applyFog, type FogOfWar } from './fog';

export interface ThemePalette { ground: THREE.Color; blocked: THREE.Color; ramp: THREE.Color; high: THREE.Color; grid: number; background: THREE.Color; wall: number; fog: THREE.Color }

// Colors from BoardView: base ground per theme, multiplied by the cell tint.
export function palette(theme: string): ThemePalette {
  const desert = theme === 'desert', forest = theme === 'forest', neon = theme === 'neon';
  return {
    ground: desert ? new THREE.Color(0.72, 0.48, 0.24) : forest ? new THREE.Color(0.24, 0.34, 0.18) : neon ? new THREE.Color(0.075, 0.095, 0.16) : new THREE.Color(0.16, 0.31, 0.31),
    blocked: desert ? new THREE.Color(0.69, 0.53, 0.39) : forest ? new THREE.Color(0.58, 0.69, 0.46) : new THREE.Color(0.35, 0.39, 0.45),
    ramp: desert ? new THREE.Color(1.09, 0.98, 0.8) : forest ? new THREE.Color(1.14, 1.01, 0.73) : new THREE.Color(0.75, 0.72, 0.45),
    high: desert ? new THREE.Color(1.16, 1.04, 0.84) : forest ? new THREE.Color(1.05, 1.13, 0.9) : new THREE.Color(0.8, 0.88, 0.77),
    grid: desert ? 0.3 : forest ? 0.25 : neon ? 0.45 : 1,
    background: desert ? new THREE.Color(0.05, 0.035, 0.025) : forest ? new THREE.Color(0.012, 0.018, 0.012) : neon ? new THREE.Color(0.005, 0.006, 0.014) : new THREE.Color(0.008, 0.014, 0.018),
    fog: (desert ? new THREE.Color(0.52, 0.43, 0.32) : forest ? new THREE.Color(0.3, 0.39, 0.29) : neon ? new THREE.Color(0.28, 0.29, 0.42) : new THREE.Color(0.28, 0.36, 0.4)).multiplyScalar(0.32),
    wall: desert ? 0.82 : 0.7,
  };
}

const hash = (x: number, y: number) => { let h = (x * 374761393 + y * 668265263) | 0; h = (h ^ (h >>> 13)) * 1274126177; return ((h ^ (h >>> 16)) >>> 0) / 4294967295; };

export interface TerrainOverlays {
  placement: THREE.DataTexture;
  placementData: Uint8Array;
  pulse: THREE.Vector4; pulseColor: THREE.Color;
  area: THREE.Vector4; areaColor: THREE.Color;
  range: THREE.Vector4;
  cursor: THREE.Vector4; cursorColor: THREE.Color;
}

// Per-cell quads plus cliff faces on exposed edges, exactly like BoardView's mesh.
export function buildTerrain(terrain: Terrain, fog: FogOfWar, theme: string): { mesh: THREE.Mesh; overlays: TerrainOverlays; skirt: THREE.Mesh } {
  const colors = palette(theme);
  const positions: number[] = [], vertexColors: number[] = [], indices: number[] = [];
  const quad = (a: number[], b: number[], c: number[], d: number[], tint: THREE.Color) => {
    const n = positions.length / 3;
    for (const v of [a, b, c, d]) { positions.push(v[0], v[1], -v[2]); vertexColors.push(tint.r, tint.g, tint.b); }
    // Reversed winding because map y becomes -z.
    indices.push(n, n + 2, n + 1, n, n + 3, n + 2);
  };
  const { width, height } = terrain;
  const tint = new THREE.Color();
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    const i = terrain.index(x, y);
    const cell = !terrain.walkable[i] ? colors.blocked : terrain.ramp[i] ? colors.ramp : terrain.elevation[i] > 0 ? colors.high : new THREE.Color(1, 1, 1);
    const variation = 0.94 + hash(x, y) * 0.1;
    tint.copy(colors.ground).multiply(cell).multiplyScalar(variation);
    const h = (vx: number, vy: number) => terrain.surfaceHeight(i, vx, vy);
    quad([x, h(x, y), y], [x, h(x, y + 1), y + 1], [x + 1, h(x + 1, y + 1), y + 1], [x + 1, h(x + 1, y), y], tint);
    const wall = tint.clone().multiplyScalar(colors.wall);
    const edge = (ax: number, ay: number, bx: number, by: number, nx: number, ny: number) => {
      const neighbor = terrain.inside(nx, ny) ? terrain.index(nx, ny) : -1;
      const a = h(ax, ay), b = h(bx, by);
      const lowA = neighbor < 0 ? 0 : terrain.surfaceHeight(neighbor, ax, ay), lowB = neighbor < 0 ? 0 : terrain.surfaceHeight(neighbor, bx, by);
      if (a <= lowA + 0.001 && b <= lowB + 0.001) return;
      quad([ax, Math.min(a, lowA), ay], [ax, a, ay], [bx, b, by], [bx, Math.min(b, lowB), by], wall);
    };
    edge(x, y, x + 1, y, x, y - 1); edge(x + 1, y + 1, x, y + 1, x, y + 1);
    edge(x, y + 1, x, y, x - 1, y); edge(x + 1, y, x + 1, y + 1, x + 1, y);
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.Float32BufferAttribute(vertexColors, 3));
  geometry.setIndex(indices);
  geometry.computeVertexNormals();
  geometry.computeBoundingSphere();

  const placementData = new Uint8Array(width * height * 4);
  const placement = new THREE.DataTexture(placementData, width, height, THREE.RGBAFormat, THREE.UnsignedByteType);
  placement.magFilter = THREE.NearestFilter; placement.minFilter = THREE.NearestFilter; placement.needsUpdate = true;
  const overlays: TerrainOverlays = {
    placement, placementData,
    pulse: new THREE.Vector4(), pulseColor: new THREE.Color(0.3, 0.95, 0.94),
    area: new THREE.Vector4(), areaColor: new THREE.Color(0.85, 0.4, 1),
    range: new THREE.Vector4(),
    cursor: new THREE.Vector4(), cursorColor: new THREE.Color(1, 0.3, 0.24),
  };
  const material = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.96, metalness: 0 });
  applyFog(material, fog, (shader) => {
    Object.assign(shader.uniforms, {
      uPlacement: { value: overlays.placement }, uGrid: { value: colors.grid },
      uPulse: { value: overlays.pulse }, uPulseColor: { value: overlays.pulseColor },
      uArea: { value: overlays.area }, uAreaColor: { value: overlays.areaColor }, uRange: { value: overlays.range },
      uCursor: { value: overlays.cursor }, uCursorColor: { value: overlays.cursorColor },
    });
    shader.fragmentShader = shader.fragmentShader
      .replace('varying vec2 vMapPos;', `varying vec2 vMapPos;
uniform sampler2D uPlacement; uniform float uGrid;
uniform vec4 uPulse; uniform vec3 uPulseColor; uniform vec4 uArea; uniform vec3 uAreaColor; uniform vec4 uRange;
uniform vec4 uCursor; uniform vec3 uCursorColor;
float frontierRing(vec4 ring, float width) {
  if (ring.w <= 0.0) return 0.0;
  float d = abs(distance(vMapPos, ring.xy) - ring.z);
  return (1.0 - smoothstep(width * 0.5, width, d)) * ring.w;
}`)
      .replace('#include <dithering_fragment>', `vec2 frontierCell = abs(fract(vMapPos) - 0.5);
  float frontierEdge = max(frontierCell.x, frontierCell.y);
  float frontierLine = smoothstep(0.5 - fwidth(vMapPos.x) * 1.2, 0.5, frontierEdge);
  gl_FragColor.rgb *= 1.0 - frontierLine * 0.16 * uGrid;
  vec4 frontierPlace = texture2D(uPlacement, vMapPos / uMapSize);
  gl_FragColor.rgb = mix(gl_FragColor.rgb, frontierPlace.rgb, frontierPlace.a);
  gl_FragColor.rgb = mix(gl_FragColor.rgb, uPulseColor, frontierRing(uPulse, 0.16));
  gl_FragColor.rgb = mix(gl_FragColor.rgb, uAreaColor, frontierRing(uArea, 0.16) + (uArea.w > 0.0 && distance(vMapPos, uArea.xy) < uArea.z ? 0.18 * uArea.w : 0.0));
  gl_FragColor.rgb = mix(gl_FragColor.rgb, uAreaColor * 0.8, frontierRing(uRange, 0.1) * 0.8);
  gl_FragColor.rgb = mix(gl_FragColor.rgb, uCursorColor, frontierRing(uCursor, 0.12));
#include <dithering_fragment>`);
  });
  const mesh = new THREE.Mesh(geometry, material);
  mesh.receiveShadow = true;
  mesh.name = 'terrain';

  // Dark ground around the board so the edges never float in a void.
  const skirtGeometry = new THREE.PlaneGeometry(width + 400, height + 400);
  skirtGeometry.rotateX(-Math.PI / 2);
  const skirt = new THREE.Mesh(skirtGeometry, new THREE.MeshBasicMaterial({ color: colors.background }));
  skirt.position.set(width / 2, -0.04, -height / 2);
  return { mesh, overlays, skirt };
}
