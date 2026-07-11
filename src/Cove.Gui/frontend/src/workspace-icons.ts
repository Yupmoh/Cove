export interface WorkspaceIcon {
  kind: string;
  value: string;
}

export const WORKSPACE_ICON_CHOICES = [
  "📁", "🚀", "🛠️", "🧪", "🌊", "🔥", "⚡", "🎨",
  "🧠", "📦", "🌱", "🕹️", "📚", "💎", "🐚", "⭐",
] as const;

export function workspaceGlyph(icon: WorkspaceIcon | null | undefined): string | null {
  if (!icon) return null;
  if (icon.kind !== "emoji") return null;
  if (!icon.value) return null;
  return icon.value;
}
