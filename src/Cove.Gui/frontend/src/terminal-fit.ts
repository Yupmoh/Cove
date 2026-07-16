export interface TermDims {
  cols: number;
  rows: number;
}

export function isPaneFittable(width: number, height: number, connected: boolean, visible: boolean): boolean {
  return connected && visible && width > 0 && height > 0;
}

export function shouldResize(next: TermDims, prev: TermDims | null, visible: boolean): boolean {
  if (!visible) return false;
  if (next.cols < 1 || next.rows < 1) return false;
  if (!prev) return true;
  return prev.cols !== next.cols || prev.rows !== next.rows;
}

export function scrollLineAfterFit(before: { baseY: number; viewportY: number }, nextBaseY: number): number {
  const distanceFromBottom = Math.max(0, before.baseY - before.viewportY);
  return Math.max(0, nextBaseY - distanceFromBottom);
}

export function viewportScrollTopFor(
  targetLine: number,
  baseY: number,
  scrollHeight: number,
  clientHeight: number,
): number | null {
  if (baseY <= 0) return null;
  const max = scrollHeight - clientHeight;
  if (max <= 0) return null;
  return (Math.min(targetLine, baseY) / baseY) * max;
}
