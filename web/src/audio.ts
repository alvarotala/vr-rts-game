import { musicUrls } from 'virtual:frontier-content';

export type Sound = 'click' | 'select' | 'order' | 'error' | 'complete' | 'shot' | 'heavy' | 'death' | 'explosion' | 'victory' | 'defeat';

// Map music (the same tracks as the Quest build) and small synthesized effects.
export class AudioSystem {
  private context: AudioContext | null = null;
  private music: HTMLAudioElement | null = null;
  private lastShot = 0;
  private shots = 0;
  musicVolume = 0.35;
  effectsVolume = 0.65;

  private ctx(): AudioContext | null {
    if (!this.context) { try { this.context = new AudioContext(); } catch { return null; } }
    if (this.context.state === 'suspended') void this.context.resume();
    return this.context;
  }

  playMusic(mapId: string): void {
    const url = musicUrls[mapId];
    if (!url) return;
    if (this.music && this.music.dataset.map === mapId) { void this.music.play().catch(() => undefined); return; }
    this.stopMusic();
    const audio = new Audio(url);
    audio.loop = true; audio.volume = this.musicVolume; audio.dataset.map = mapId;
    this.music = audio;
    void audio.play().catch(() => undefined);
  }
  stopMusic(): void { if (this.music) { this.music.pause(); this.music = null; } }
  setMusicVolume(value: number): void { this.musicVolume = value; if (this.music) this.music.volume = value; }

  play(sound: Sound): void {
    if (this.effectsVolume <= 0) return;
    const ctx = this.ctx();
    if (!ctx) return;
    const now = ctx.currentTime;
    if (sound === 'shot' || sound === 'heavy') {
      if (now - this.lastShot > 0.1) { this.lastShot = now; this.shots = 0; }
      if (++this.shots > 4) return;
    }
    const gain = ctx.createGain();
    gain.connect(ctx.destination);
    const tone = (frequency: number, start: number, duration: number, type: OscillatorType = 'sine', volume = 0.2, slide = 0) => {
      const osc = ctx.createOscillator(); const g = ctx.createGain();
      osc.type = type; osc.frequency.setValueAtTime(frequency, now + start);
      if (slide) osc.frequency.exponentialRampToValueAtTime(Math.max(30, frequency * slide), now + start + duration);
      g.gain.setValueAtTime(0.0001, now + start); g.gain.exponentialRampToValueAtTime(volume * this.effectsVolume, now + start + 0.01);
      g.gain.exponentialRampToValueAtTime(0.0001, now + start + duration);
      osc.connect(g).connect(gain); osc.start(now + start); osc.stop(now + start + duration + 0.02);
    };
    const noise = (duration: number, volume: number, frequency: number) => {
      const buffer = ctx.createBuffer(1, Math.max(1, Math.floor(ctx.sampleRate * duration)), ctx.sampleRate);
      const data = buffer.getChannelData(0);
      for (let i = 0; i < data.length; i++) data[i] = (Math.random() * 2 - 1) * (1 - i / data.length);
      const source = ctx.createBufferSource(); source.buffer = buffer;
      const filter = ctx.createBiquadFilter(); filter.type = 'lowpass'; filter.frequency.value = frequency;
      const g = ctx.createGain(); g.gain.value = volume * this.effectsVolume;
      source.connect(filter).connect(g).connect(gain); source.start(now);
    };
    switch (sound) {
      case 'click': tone(880, 0, 0.05, 'triangle', 0.08); break;
      case 'select': tone(660, 0, 0.06, 'triangle', 0.1); tone(990, 0.04, 0.06, 'triangle', 0.07); break;
      case 'order': tone(520, 0, 0.07, 'square', 0.05); tone(780, 0.05, 0.08, 'square', 0.04); break;
      case 'error': tone(180, 0, 0.18, 'sawtooth', 0.08, 0.7); break;
      case 'complete': tone(660, 0, 0.12, 'sine', 0.12); tone(880, 0.1, 0.12, 'sine', 0.12); tone(1320, 0.2, 0.2, 'sine', 0.1); break;
      case 'shot': noise(0.05, 0.05, 2400); break;
      case 'heavy': noise(0.3, 0.12, 600); tone(90, 0, 0.3, 'sine', 0.12, 0.5); break;
      case 'death': noise(0.18, 0.08, 900); break;
      case 'explosion': noise(0.6, 0.16, 400); tone(60, 0, 0.5, 'sine', 0.16, 0.5); break;
      case 'victory': [523, 659, 784, 1046].forEach((f, i) => tone(f, i * 0.14, 0.4, 'triangle', 0.12)); break;
      case 'defeat': [392, 330, 262, 196].forEach((f, i) => tone(f, i * 0.18, 0.45, 'triangle', 0.1)); break;
    }
  }
}
