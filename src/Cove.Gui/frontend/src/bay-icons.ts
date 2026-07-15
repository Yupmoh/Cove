export interface BayIcon {
  kind: string;
  value: string;
}

export const BAY_ICON_CHOICES = [
  "📁", "🚀", "🛠️", "🧪", "🌊", "🔥", "⚡", "🎨",
  "🧠", "📦", "🌱", "🕹️", "📚", "💎", "🐚", "⭐",
  "💻", "🖥️", "⌨️", "⚙️", "🔧", "🧰", "🧩", "🧱",
  "🗃️", "🗂️", "📊", "🔬", "🛰️", "🌐", "☁️", "🔮",
] as const;

export function bayGlyph(icon: BayIcon | null | undefined): string | null {
  if (!icon) return null;
  if (icon.kind !== "emoji") return null;
  if (!icon.value) return null;
  return icon.value;
}
