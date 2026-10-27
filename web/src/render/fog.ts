import * as THREE from 'three';

// Shared fog-of-war texture (one texel per map cell): 0 unexplored, ~0.43 explored, 1 visible.
export class FogOfWar {
  readonly texture: THREE.DataTexture;
  readonly size: THREE.Vector2;
  readonly color = new THREE.Color(0.14, 0.18, 0.2);
  private readonly data: Uint8Array;
  private lastTick = -1;
  constructor(readonly width: number, readonly height: number) {
    this.data = new Uint8Array(width * height).fill(255);
    this.texture = new THREE.DataTexture(this.data, width, height, THREE.RedFormat, THREE.UnsignedByteType);
    this.texture.magFilter = THREE.LinearFilter; this.texture.minFilter = THREE.LinearFilter;
    this.texture.needsUpdate = true;
    this.size = new THREE.Vector2(width, height);
  }
  update(visibility: Uint8Array, tick: number): void {
    if (tick === this.lastTick || visibility.length !== this.data.length) return;
    this.lastTick = tick;
    for (let i = 0; i < this.data.length; i++) { const v = visibility[i]; this.data[i] = v === 2 ? 255 : v === 1 ? 110 : 0; }
    this.texture.needsUpdate = true;
  }
  revealAll(): void { this.data.fill(255); this.texture.needsUpdate = true; this.lastTick = -1; }
  value(x: number, y: number): number {
    const cx = Math.floor(x), cy = Math.floor(y);
    if (cx < 0 || cy < 0 || cx >= this.width || cy >= this.height) return 0;
    return this.data[cy * this.width + cx];
  }
  dispose() { this.texture.dispose(); }
}

// Darkens any standard/lambert material by the fog texture at its world position.
export function applyFog(material: THREE.Material, fog: FogOfWar, extra?: (shader: THREE.WebGLProgramParametersWithUniforms) => void): void {
  const previous = material.onBeforeCompile;
  material.onBeforeCompile = (shader, renderer) => {
    previous.call(material, shader, renderer);
    shader.uniforms.uFogTex = { value: fog.texture };
    shader.uniforms.uMapSize = { value: fog.size };
    shader.uniforms.uFogColor = { value: fog.color };
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', '#include <common>\nvarying vec2 vMapPos;')
      .replace('#include <project_vertex>', `#include <project_vertex>
  vec4 frontierWorld = vec4(transformed, 1.0);
  #ifdef USE_INSTANCING
    frontierWorld = instanceMatrix * frontierWorld;
  #endif
  frontierWorld = modelMatrix * frontierWorld;
  vMapPos = vec2(frontierWorld.x, -frontierWorld.z);`);
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', '#include <common>\nuniform sampler2D uFogTex;\nuniform vec2 uMapSize;\nuniform vec3 uFogColor;\nvarying vec2 vMapPos;')
      .replace('#include <dithering_fragment>', `float frontierFog = texture2D(uFogTex, vMapPos / uMapSize).r;
  vec3 frontierHaze = mix(uFogColor, gl_FragColor.rgb * 0.48, smoothstep(0.0, 0.43, frontierFog));
  gl_FragColor.rgb = mix(frontierHaze, gl_FragColor.rgb, smoothstep(0.43, 1.0, frontierFog));
#include <dithering_fragment>`);
    extra?.(shader);
  };
  material.customProgramCacheKey = () => 'frontier-fog-' + (extra ? 'x' : '');
}
