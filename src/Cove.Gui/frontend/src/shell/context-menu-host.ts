import {
  activeItem,
  clampMenuPosition,
  firstSelectableIndex,
  moveSelection,
  normalizeItems,
  type ContextMenuItem,
  type ContextMenuModel,
} from "../context-menu";
import type { ComponentHandle } from "../app/lifecycle";

export class ContextMenuHost implements ComponentHandle {
  private menu: HTMLElement | null = null;
  private keyHandler: ((event: KeyboardEvent) => void) | null = null;
  private awayHandler: ((event: MouseEvent) => void) | null = null;
  private awayTimer: ReturnType<typeof globalThis.setTimeout> | null = null;
  private disposed = false;

  constructor(private readonly document: Document) {}

  open(model: ContextMenuModel, onSelect: (id: string) => void): void {
    if (this.disposed) throw new Error("ContextMenuHost is disposed");
    this.close();
    const items = normalizeItems(model.items);
    if (items.length === 0) {
      console.warn("context menu requested without selectable content");
      return;
    }
    const menu = this.document.createElement("div");
    menu.className = "ctx-menu";
    let selected = firstSelectableIndex(items);
    const rows: HTMLElement[] = [];
    const paint = () => rows.forEach((row, index) => row.classList.toggle("sel", index === selected));
    const choose = (index: number) => {
      const item = activeItem(items, index);
      if (!item) {
        console.warn("context menu selection has no active item", index);
        return;
      }
      this.close();
      onSelect(item.id);
    };
    items.forEach((item, index) => {
      if (item.separator) {
        const separator = this.document.createElement("div");
        separator.className = "ctx-sep";
        rows.push(separator);
        menu.appendChild(separator);
        return;
      }
      const row = this.document.createElement("div");
      row.className = "ctx-item" + (item.danger ? " danger" : "") + (item.disabled ? " disabled" : "");
      row.textContent = item.label;
      rows.push(row);
      if (!item.disabled) {
        row.addEventListener("mouseenter", () => {
          selected = index;
          paint();
        });
        row.addEventListener("click", () => choose(index));
      }
      menu.appendChild(row);
    });
    paint();
    menu.style.cssText = "position:fixed;left:-9999px;top:-9999px;";
    this.document.body.appendChild(menu);
    const viewport = this.document.defaultView;
    const position = clampMenuPosition(
      { x: model.x, y: model.y },
      { width: menu.offsetWidth, height: menu.offsetHeight },
      { width: viewport?.innerWidth ?? 0, height: viewport?.innerHeight ?? 0 },
    );
    menu.style.left = `${position.x}px`;
    menu.style.top = `${position.y}px`;
    this.menu = menu;
    this.keyHandler = (event) => {
      if (event.key === "Escape") {
        event.preventDefault();
        this.close();
      } else if (event.key === "ArrowDown") {
        event.preventDefault();
        selected = moveSelection(items, selected, 1);
        paint();
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        selected = moveSelection(items, selected, -1);
        paint();
      } else if (event.key === "Enter") {
        event.preventDefault();
        choose(selected);
      }
    };
    this.document.addEventListener("keydown", this.keyHandler, true);
    this.awayHandler = (event) => {
      if (this.menu && !this.menu.contains(event.target as Node)) this.close();
    };
    this.awayTimer = globalThis.setTimeout(() => {
      this.awayTimer = null;
      if (this.awayHandler) this.document.addEventListener("mousedown", this.awayHandler, true);
    }, 0);
  }

  openAt(event: MouseEvent, items: ContextMenuItem[], onSelect: (id: string) => void): void {
    event.preventDefault();
    event.stopPropagation();
    this.open({ items, x: event.clientX, y: event.clientY }, onSelect);
  }

  close(): void {
    this.menu?.remove();
    this.menu = null;
    if (this.keyHandler) this.document.removeEventListener("keydown", this.keyHandler, true);
    if (this.awayHandler) this.document.removeEventListener("mousedown", this.awayHandler, true);
    if (this.awayTimer !== null) globalThis.clearTimeout(this.awayTimer);
    this.keyHandler = null;
    this.awayHandler = null;
    this.awayTimer = null;
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.close();
  }
}
