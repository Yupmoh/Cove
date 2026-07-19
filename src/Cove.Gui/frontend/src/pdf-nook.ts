import { mediaUrl } from "./media-url";
import { LifecycleScope, type NookContentHandle } from "./app/lifecycle";

export function renderPdfNook(filePath: string): NookContentHandle {
  const lifecycle = new LifecycleScope();
  const el = document.createElement("div");
  el.className = "pdf-nook";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0d1117;color:#e6edf3;";

  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;border-bottom:1px solid #303030;display:flex;gap:8px;align-items:center;flex-shrink:0;font-size:13px;font-weight:600;";
  header.textContent = filePath.split("/").pop() || filePath;
  el.appendChild(header);

  const frame = document.createElement("iframe");
  frame.title = filePath;
  frame.style.cssText = "flex:1;min-height:0;border:none;width:100%;background:#525659;";
  el.appendChild(frame);

  mediaUrl(filePath).then((url) => {
    if (lifecycle.isDisposed) return;
    frame.src = url;
  }).catch((err) => {
    if (lifecycle.isDisposed) return;
    console.warn("pdf media lease failed", filePath, err);
    header.textContent = `Failed to open ${filePath.split("/").pop() || filePath}`;
  });

  lifecycle.own(() => {
    frame.removeAttribute("src");
    frame.src = "about:blank";
  });
  return { element: el, dispose: () => lifecycle.dispose() };
}
