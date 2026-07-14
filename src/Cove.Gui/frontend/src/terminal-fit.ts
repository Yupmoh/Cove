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
