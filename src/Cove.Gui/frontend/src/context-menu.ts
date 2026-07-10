export interface ContextMenuItem {
  id: string;
  label: string;
  danger?: boolean;
  disabled?: boolean;
  separator?: boolean;
}

export interface ContextMenuModel {
  items: ContextMenuItem[];
  x: number;
  y: number;
}

export interface Rect {
  width: number;
  height: number;
}

export interface Point {
  x: number;
  y: number;
}

export const CONTEXT_MENU_MARGIN = 6;

export function clampMenuPosition(anchor: Point, size: Rect, viewport: Rect, margin = CONTEXT_MENU_MARGIN): Point {
  let x = anchor.x;
  if (x + size.width > viewport.width - margin) x = anchor.x - size.width;
  if (x < margin) x = margin;
  if (x + size.width > viewport.width - margin) x = Math.max(margin, viewport.width - margin - size.width);
  let y = anchor.y;
  if (y + size.height > viewport.height - margin) y = anchor.y - size.height;
  if (y < margin) y = margin;
  if (y + size.height > viewport.height - margin) y = Math.max(margin, viewport.height - margin - size.height);
  return { x, y };
}

export function isSelectable(item: ContextMenuItem): boolean {
  return item.separator !== true && item.disabled !== true;
}

export function normalizeItems(items: ContextMenuItem[]): ContextMenuItem[] {
  const out: ContextMenuItem[] = [];
  for (const item of items) {
    if (item.separator) {
      if (out.length === 0) continue;
      if (out[out.length - 1].separator) continue;
      out.push(item);
    } else {
      out.push(item);
    }
  }
  while (out.length > 0 && out[out.length - 1].separator) out.pop();
  return out;
}

export function firstSelectableIndex(items: ContextMenuItem[]): number {
  for (let i = 0; i < items.length; i++) {
    if (isSelectable(items[i])) return i;
  }
  return -1;
}

export function moveSelection(items: ContextMenuItem[], current: number, dir: 1 | -1): number {
  const n = items.length;
  if (n === 0) return -1;
  let idx = current;
  if (idx < 0 || idx >= n) idx = dir === 1 ? -1 : 0;
  for (let step = 0; step < n; step++) {
    idx = (idx + dir + n) % n;
    if (isSelectable(items[idx])) return idx;
  }
  return -1;
}

export function activeItem(items: ContextMenuItem[], index: number): ContextMenuItem | null {
  if (index < 0 || index >= items.length) return null;
  const item = items[index];
  return isSelectable(item) ? item : null;
}
