const EXTENSION_MAP: Record<string, string> = {
  ts: "typescript", tsx: "typescript", js: "javascript", jsx: "javascript",
  cs: "csharp", json: "json", md: "markdown", markdown: "markdown",
  py: "python", rs: "rust", go: "go", java: "java", kt: "kotlin",
  rb: "ruby", php: "php", c: "c", h: "c", cpp: "cpp", cc: "cpp", hpp: "cpp",
  css: "css", scss: "scss", less: "less", html: "html", xml: "xml",
  yaml: "yaml", yml: "yaml", toml: "toml", ini: "ini", sh: "shell",
  bash: "shell", zsh: "shell", sql: "sql", swift: "swift", dart: "dart",
  scala: "scala", lua: "lua", r: "r", dockerfile: "dockerfile",
};

export function detectLanguage(filePath: string): string {
  const lower = filePath.toLowerCase();
  if (lower === "dockerfile" || lower.endsWith("/dockerfile")) return "dockerfile";
  const dotIdx = lower.lastIndexOf(".");
  if (dotIdx < 0 || dotIdx === lower.length - 1) return "plaintext";
  const ext = lower.slice(dotIdx + 1);
  return EXTENSION_MAP[ext] ?? "plaintext";
}
