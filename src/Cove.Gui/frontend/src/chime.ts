export type ChimeKind = "done" | "needs-input";

export const AGENT_CHIMES_STORAGE_KEY = "cove.sound.agentChimes";

export function chimesEnabledFrom(raw: string | null): boolean {
  return raw !== "false";
}

export function chimePrefValue(enabled: boolean): string {
  return enabled ? "true" : "false";
}

export interface ChimeNote {
  freq: number;
  start: number;
  dur: number;
  gain: number;
}

export function chimePlan(kind: ChimeKind): ChimeNote[] {
  if (kind === "done") {
    return [
      { freq: 660, start: 0, dur: 0.16, gain: 0.09 },
      { freq: 880, start: 0.12, dur: 0.3, gain: 0.09 },
    ];
  }
  return [
    { freq: 740, start: 0, dur: 0.14, gain: 0.1 },
    { freq: 740, start: 0.22, dur: 0.14, gain: 0.1 },
  ];
}

export function detectChimes(prev: ReadonlyMap<string, string>, next: ReadonlyMap<string, string>): ChimeKind[] {
  if (prev.size === 0) return [];
  const kinds: ChimeKind[] = [];
  for (const [id, state] of next) {
    if (prev.get(id) === state) continue;
    if (state === "needs-input" || state === "done") kinds.push(state);
  }
  return kinds;
}

let audioCtx: AudioContext | null = null;

export function playChime(kind: ChimeKind): void {
  try {
    audioCtx ??= new AudioContext();
    const ctx = audioCtx;
    if (ctx.state === "suspended") void ctx.resume();
    const now = ctx.currentTime;
    for (const note of chimePlan(kind)) {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = "sine";
      osc.frequency.value = note.freq;
      gain.gain.setValueAtTime(0, now + note.start);
      gain.gain.linearRampToValueAtTime(note.gain, now + note.start + 0.015);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + note.start + note.dur);
      osc.connect(gain).connect(ctx.destination);
      osc.start(now + note.start);
      osc.stop(now + note.start + note.dur + 0.05);
    }
  } catch (err) {
    console.warn("chime playback failed", err);
  }
}
