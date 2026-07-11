import { mediaUrl } from "./media-url";

export function renderPdfNook(filePath: string): HTMLElement {
  const el = document.createElement("div");
  el.className = "pdf-nook";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0d1117;color:#e6edf3;";

  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;border-bottom:1px solid #303030;display:flex;gap:8px;align-items:center;flex-shrink:0;font-size:13px;font-weight:600;";
  header.textContent = filePath.split("/").pop() || filePath;
  el.appendChild(header);

  const frame = document.createElement("iframe");
  frame.title = filePath;
  frame.src = mediaUrl(filePath);
  frame.style.cssText = "flex:1;min-height:0;border:none;width:100%;background:#525659;";
  el.appendChild(frame);

  return el;
}
