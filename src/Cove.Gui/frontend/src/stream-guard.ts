export interface StreamGenerations {
  claim(nookId: string): number;
  invalidate(nookId: string): void;
  isCurrent(nookId: string, generation: number): boolean;
}

export function shouldDisposeNook(state: { inLayout: boolean; wsClosed: boolean }): boolean {
  return !state.inLayout && state.wsClosed;
}

export function shouldResetReplay(origin: { locallySpawned: boolean; renderedBefore: boolean }): boolean {
  return origin.renderedBefore || !origin.locallySpawned;
}

export type ReplayViewportAction = "bottom" | "preserve";

export function replayViewportAction(state: { resetOnReplay: boolean; resynced: boolean }): ReplayViewportAction {
  return state.resetOnReplay || state.resynced ? "bottom" : "preserve";
}

export type StreamVisibilityAction = "connect" | "disconnect" | "none";

export function streamVisibilityAction(state: { visible: boolean; connected: boolean }): StreamVisibilityAction {
  if (state.visible && !state.connected) return "connect";
  if (!state.visible && state.connected) return "disconnect";
  return "none";
}

export function createStreamGenerations(): StreamGenerations {
  const generations = new Map<string, number>();
  const bump = (nookId: string): number => {
    const next = (generations.get(nookId) ?? 0) + 1;
    generations.set(nookId, next);
    return next;
  };
  return {
    claim(nookId) { return bump(nookId); },
    invalidate(nookId) { bump(nookId); },
    isCurrent(nookId, generation) { return generations.get(nookId) === generation; },
  };
}
