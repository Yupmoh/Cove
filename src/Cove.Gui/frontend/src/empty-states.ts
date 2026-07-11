export interface EmptyStateConfig {
  message: string;
  actionLabel?: string;
  actionIcon?: string;
}

export function buildEmptyState(config: EmptyStateConfig): HTMLElement {
  const el = document.createElement("div");
  el.className = "cove-empty-state";
  el.style.cssText = "display:flex;flex-direction:column;align-items:center;justify-content:center;height:100%;gap:12px;color:var(--muted);font-size:13px;text-align:center;padding:24px;";

  const msg = document.createElement("div");
  msg.textContent = config.message;
  el.appendChild(msg);

  if (config.actionLabel) {
    const action = document.createElement("div");
    action.className = "cove-empty-action";
    action.style.cssText = "color:var(--accent);cursor:pointer;font-size:12px;padding:6px 14px;border:1px solid var(--accent-dim);border-radius:6px;transition:background 0.15s;";
    if (config.actionIcon) {
      const icon = document.createElement("span");
      icon.textContent = config.actionIcon + " ";
      action.appendChild(icon);
    }
    const label = document.createElement("span");
    label.textContent = config.actionLabel;
    action.appendChild(label);
    action.addEventListener("mouseenter", () => action.style.background = "color-mix(in srgb, var(--accent) 12%, transparent)");
    action.addEventListener("mouseleave", () => action.style.background = "transparent");
    el.appendChild(action);
  }

  return el;
}

export const EmptyStateMessages = {
  noShores: "No shores yet",
  noNotes: "No notes yet",
  noSearchResults: "No results",
  noChanges: "No changes",
  noTasks: "No tasks",
  noteDeleted: "This note was deleted",
  noTimeline: "No activity yet",
} as const;
