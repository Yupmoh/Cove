export interface ChromeVisibility {
  leftSidebarHidden: boolean;
  toolbarHidden: boolean;
  notepadOpen: boolean;
}

export interface ZenState {
  active: boolean;
  saved: ChromeVisibility | null;
}

export interface ZenTransition {
  state: ZenState;
  visibility: ChromeVisibility;
}

const ALL_SHOWN: ChromeVisibility = { leftSidebarHidden: false, toolbarHidden: false, notepadOpen: false };
const ALL_HIDDEN: ChromeVisibility = { leftSidebarHidden: true, toolbarHidden: true, notepadOpen: false };

export function initialZenState(): ZenState {
  return { active: false, saved: null };
}

export function enterZen(state: ZenState, current: ChromeVisibility): ZenTransition {
  const saved = state.active && state.saved ? state.saved : { ...current };
  return { state: { active: true, saved }, visibility: { ...ALL_HIDDEN } };
}

export function exitZen(state: ZenState): ZenTransition {
  const visibility = state.saved ? { ...state.saved } : { ...ALL_SHOWN };
  return { state: { active: false, saved: null }, visibility };
}

export function toggleZen(state: ZenState, current: ChromeVisibility): ZenTransition {
  return state.active ? exitZen(state) : enterZen(state, current);
}
