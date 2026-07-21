export interface BayIcon {
  kind: string;
  value: string;
}

export interface BayMarkChoice {
  id: string;
  label: string;
  svg: string;
}

const ORBIT: BayMarkChoice = { id: "orbit", label: "Orbit", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"><circle cx="12" cy="12" r="2.2"/><ellipse cx="12" cy="12" rx="8" ry="3.8" transform="rotate(-28 12 12)"/><circle cx="18.8" cy="8.1" r="1" fill="currentColor" stroke="none"/></svg>' };
const PRISM: BayMarkChoice = { id: "prism", label: "Prism", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"><path d="m12 3 8 16H4L12 3Z"/><path d="m12 3 2.2 16M8.5 12h8"/></svg>' };
const SPARK: BayMarkChoice = { id: "spark", label: "Spark", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"><path d="m12 2.8 1.7 6.6 6.5 2.6-6.5 2.6-1.7 6.6-1.7-6.6L3.8 12l6.5-2.6L12 2.8Z"/></svg>' };
const WAVE: BayMarkChoice = { id: "wave", label: "Wave", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"><path d="M3 9c3.2-3 5.8-3 9 0s5.8 3 9 0M3 15c3.2-3 5.8-3 9 0s5.8 3 9 0"/></svg>' };
const GRID: BayMarkChoice = { id: "grid", label: "Grid", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6"><rect x="4" y="4" width="6" height="6" rx="1.5"/><rect x="14" y="4" width="6" height="6" rx="1.5"/><rect x="4" y="14" width="6" height="6" rx="1.5"/><rect x="14" y="14" width="6" height="6" rx="1.5"/></svg>' };
const PORTAL: BayMarkChoice = { id: "portal", label: "Portal", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"><path d="M7.1 19.1A9 9 0 1 1 19 17"/><path d="M9.2 15.8a5 5 0 1 1 6.6-1.1"/><circle cx="12" cy="12" r="1.4" fill="currentColor" stroke="none"/></svg>' };
const BRANCH: BayMarkChoice = { id: "branch", label: "Branch", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"><circle cx="7" cy="5" r="2"/><circle cx="17" cy="8" r="2"/><circle cx="8" cy="19" r="2"/><path d="M7 7v5a6 6 0 0 0 6 6h2M7 11h4a6 6 0 0 0 6-6"/></svg>' };
const RUNE: BayMarkChoice = { id: "rune", label: "Rune", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"><path d="m12 3 7 9-7 9-7-9 7-9Z"/><path d="m12 7 3 5-3 5-3-5 3-5Z"/></svg>' };
const HORIZON: BayMarkChoice = { id: "horizon", label: "Horizon", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"><path d="M3 15h18M6 15a6 6 0 0 1 12 0M8 19h8"/></svg>' };
const PULSE: BayMarkChoice = { id: "pulse", label: "Pulse", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"><path d="M3 12h4l2-6 4 12 2-6h6"/></svg>' };
const STACK: BayMarkChoice = { id: "stack", label: "Stack", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"><path d="m12 3 9 5-9 5-9-5 9-5Z"/><path d="m5 12 7 4 7-4M5 16l7 4 7-4"/></svg>' };
const HELIX: BayMarkChoice = { id: "helix", label: "Helix", svg: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"><path d="M7 3c0 6 10 6 10 12 0 3-2.5 5-5 6M17 3c0 6-10 6-10 12 0 3 2.5 5 5 6M8.5 7h7M8.5 17h7"/></svg>' };

export const BAY_ICON_CHOICES: readonly BayMarkChoice[] = [ORBIT, PRISM, SPARK, WAVE, GRID, PORTAL, BRANCH, RUNE, HORIZON, PULSE, STACK, HELIX];

const BAY_MARK_BY_ID: Record<string, BayMarkChoice> = {
  orbit: ORBIT,
  prism: PRISM,
  spark: SPARK,
  wave: WAVE,
  grid: GRID,
  portal: PORTAL,
  branch: BRANCH,
  rune: RUNE,
  horizon: HORIZON,
  pulse: PULSE,
  stack: STACK,
  helix: HELIX,
};

function legacyMark(value: string): BayMarkChoice {
  let hash = 0;
  for (const character of value) hash = (hash * 31 + (character.codePointAt(0) ?? 0)) >>> 0;
  return BAY_ICON_CHOICES[hash % BAY_ICON_CHOICES.length];
}

export function bayMark(icon: BayIcon | null | undefined): BayMarkChoice | null {
  if (!icon?.value) return null;
  if (icon.kind === "mark") return BAY_MARK_BY_ID[icon.value] ?? null;
  if (icon.kind === "emoji") return legacyMark(icon.value);
  return null;
}
