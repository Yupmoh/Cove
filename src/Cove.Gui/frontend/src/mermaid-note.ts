import { invoke } from "./invoke";

interface NoteReadResult {
  id: string;
  title: string;
  content: string;
  kind: string;
  format: string | null;
}

let currentNoteId: string | null = null;
let currentBayId: string | null = null;

export async function renderMermaidNote(bayId: string, noteId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "mermaid-note-editor";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  currentNoteId = noteId;
  currentBayId = bayId;

  try {
    const result = await invoke<NoteReadResult>("cove://commands/note.read", { bayId, id: noteId });
    el.appendChild(buildToolbar());
    el.appendChild(buildEditor(result.content));
  } catch (e) {
    el.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load mermaid note: ${(e as Error).message}</div>`;
  }

  return el;
}

function buildToolbar(): HTMLElement {
  const toolbar = document.createElement("div");
  toolbar.style.cssText = "padding:8px 12px;display:flex;gap:8px;align-items:center;border-bottom:1px solid #1e2d3f;";

  const save = document.createElement("button");
  save.textContent = "Save";
  save.style.cssText = "padding:4px 12px;background:#2563eb;border:1px solid #3b82f6;border-radius:4px;color:#fff;cursor:pointer;font-size:12px;";
  save.addEventListener("click", saveMermaid);
  toolbar.appendChild(save);

  const label = document.createElement("span");
  label.style.cssText = "font-size:12px;color:#6b7d8f;margin-left:8px;";
  label.textContent = "Mermaid Diagram";
  toolbar.appendChild(label);

  return toolbar;
}

function buildEditor(content: string): HTMLElement {
  const container = document.createElement("div");
  container.style.cssText = "flex:1;display:flex;overflow:hidden;";

  const textarea = document.createElement("textarea");
  textarea.className = "mermaid-source";
  textarea.value = content || "graph TD\n    A[Start] --> B{Decision}\n    B -->|Yes| C[Action 1]\n    B -->|No| D[Action 2]";
  textarea.style.cssText = "width:50%;padding:12px;background:#0b1622;border-right:1px solid #1e2d3f;color:#e5e9f0;font-family:'SF Mono',Monaco,monospace;font-size:13px;resize:none;outline:none;line-height:1.6;";
  textarea.spellcheck = false;
  textarea.addEventListener("input", () => renderPreview(textarea.value, preview));

  const preview = document.createElement("div");
  preview.className = "mermaid-preview";
  preview.style.cssText = "width:50%;padding:12px;overflow-y:auto;background:#1a1a2e;";

  container.appendChild(textarea);
  container.appendChild(preview);

  renderPreview(textarea.value, preview);

  return container;
}

function renderPreview(source: string, preview: HTMLElement): void {
  preview.innerHTML = "";
  const lines = source.split("\n").filter(l => l.trim());
  if (lines.length === 0) {
    preview.innerHTML = "<div style='color:#6b7d8f;font-size:12px;'>No diagram</div>";
    return;
  }

  const svg = renderMermaidSvg(lines);
  preview.innerHTML = svg;
}

function parseNodeToken(token: string): { id: string; label: string; shape: string } | null {
  const trimmed = token.trim();
  if (!trimmed) return null;

  const rectMatch = trimmed.match(/^(\w+)\[([^\]]*)\]/);
  if (rectMatch) return { id: rectMatch[1], label: rectMatch[2], shape: "rect" };

  const roundMatch = trimmed.match(/^(\w+)\(([^)]*)\)/);
  if (roundMatch) return { id: roundMatch[1], label: roundMatch[2], shape: "round" };

  const diamondMatch = trimmed.match(/^(\w+)\{([^}]*)\}/);
  if (diamondMatch) return { id: diamondMatch[1], label: diamondMatch[2], shape: "diamond" };

  const circleMatch = trimmed.match(/^(\w+)\(\(([^)]*)\)\)/);
  if (circleMatch) return { id: circleMatch[1], label: circleMatch[2], shape: "circle" };

  const plainMatch = trimmed.match(/^(\w+)/);
  if (plainMatch) return { id: plainMatch[1], label: plainMatch[1], shape: "rect" };

  return null;
}
function renderMermaidSvg(lines: string[]): string {
  const diagramType = lines[0]?.trim() || "graph TD";
  const parts = diagramType.split(/\s+/);
  const direction = parts.length > 1 ? parts[1] : "TD";

  const nodes = new Map<string, { label: string; shape: string }>();
  const edges: Array<{ from: string; to: string; label: string }> = [];

  for (let i = 1; i < lines.length; i++) {
    const line = lines[i].trim();
    if (!line) continue;

    const arrowIdx = line.indexOf("-->");
    if (arrowIdx < 0) continue;

    const leftPart = line.substring(0, arrowIdx).trim();
    const rightPart = line.substring(arrowIdx + 3).trim();

    const fromNode = parseNodeToken(leftPart);
    if (!fromNode) continue;
    if (!nodes.has(fromNode.id)) nodes.set(fromNode.id, fromNode);

    let edgeLabel = "";
    let restAfterLabel = rightPart;
    const labelMatch = rightPart.match(/^\|([^|]*)\|\s*(.*)/);
    if (labelMatch) {
      edgeLabel = labelMatch[1].trim();
      restAfterLabel = labelMatch[2];
    }

    const toNode = parseNodeToken(restAfterLabel);
    if (!toNode) continue;
    if (!nodes.has(toNode.id)) nodes.set(toNode.id, toNode);

    edges.push({ from: fromNode.id, to: toNode.id, label: edgeLabel });
  }

  const nodeArray = Array.from(nodes.entries());
  const spacing = 120;
  const startY = 40;
  const svgParts: string[] = [];

  svgParts.push(`<svg xmlns="http://www.w3.org/2000/svg" width="400" height="${nodeArray.length * spacing + 40}" viewBox="0 0 400 ${nodeArray.length * spacing + 40}">`);
  svgParts.push("<rect width=\"100%\" height=\"100%\" fill=\"#1a1a2e\"/>");

  nodeArray.forEach(([id, node], idx) => {
    const y = startY + idx * spacing;
    const x = 200;
    const label = node.label || id;

    if (node.shape === "rect") {
      svgParts.push(`<rect x="${x - 60}" y="${y - 18}" width="120" height="36" rx="4" fill="#14202e" stroke="#3b82f6" stroke-width="2"/>`);
    } else if (node.shape === "round") {
      svgParts.push(`<rect x="${x - 60}" y="${y - 18}" width="120" height="36" rx="18" fill="#14202e" stroke="#3b82f6" stroke-width="2"/>`);
    } else if (node.shape === "diamond") {
      svgParts.push(`<polygon points="${x},${y - 22} ${x + 70},${y} ${x},${y + 22} ${x - 70},${y}" fill="#14202e" stroke="#3b82f6" stroke-width="2"/>`);
    } else {
      svgParts.push(`<circle cx="${x}" cy="${y}" r="22" fill="#14202e" stroke="#3b82f6" stroke-width="2"/>`);
    }
    svgParts.push(`<text x="${x}" y="${y + 5}" text-anchor="middle" fill="#e5e9f0" font-size="12" font-family="system-ui">${label}</text>`);
  });

  edges.forEach(edge => {
    const fromIdx = nodeArray.findIndex(([id]) => id === edge.from);
    const toIdx = nodeArray.findIndex(([id]) => id === edge.to);
    if (fromIdx >= 0 && toIdx >= 0) {
      const fromY = startY + fromIdx * spacing;
      const toY = startY + toIdx * spacing;
      svgParts.push(`<line x1="200" y1="${fromY + 18}" x2="200" y2="${toY - 18}" stroke="#6b7d8f" stroke-width="1.5"/>`);
      if (edge.label) {
        const midY = (fromY + toY) / 2;
        svgParts.push(`<text x="210" y="${midY}" fill="#6b7d8f" font-size="11" font-family="system-ui">${edge.label}</text>`);
      }
    }
  });

  svgParts.push("</svg>");
  return svgParts.join("");
}

async function saveMermaid(): Promise<void> {
  if (!currentBayId || !currentNoteId) return;
  const textarea = document.querySelector(".mermaid-source") as HTMLTextAreaElement;
  if (!textarea) return;
  try {
    await invoke("cove://commands/note.write", {
      bayId: currentBayId,
      id: currentNoteId,
      content: textarea.value,
    });
  } catch (e) {
    console.error("Save failed:", e);
  }
}


export function _testRenderMermaidSvg(lines: string[]): string {
  return renderMermaidSvg(lines);
}