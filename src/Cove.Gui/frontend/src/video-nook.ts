import { mediaUrl } from "./media-url";
import { LifecycleScope, type NookContentHandle } from "./app/lifecycle";

export function renderVideoNook(filePath: string): NookContentHandle {
  const lifecycle = new LifecycleScope();
  const el = document.createElement("div");
  el.className = "video-nook";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0d1117;color:#e6edf3;";

  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;border-bottom:1px solid #303030;display:flex;gap:8px;align-items:center;flex-shrink:0;font-size:13px;font-weight:600;";
  header.textContent = filePath.split("/").pop() || filePath;
  el.appendChild(header);

  const stage = document.createElement("div");
  stage.style.cssText = "flex:1;min-height:0;display:flex;align-items:center;justify-content:center;overflow:hidden;";
  const video = document.createElement("video");
  video.controls = true;
  video.preload = "metadata";
  video.style.cssText = "max-width:100%;max-height:100%;outline:none;";
  stage.appendChild(video);
  el.appendChild(stage);

  mediaUrl(filePath).then((url) => {
    if (lifecycle.isDisposed) return;
    video.src = url;
  }).catch((err) => {
    if (lifecycle.isDisposed) return;
    console.warn("video media lease failed", filePath, err);
    header.textContent = `Failed to open ${filePath.split("/").pop() || filePath}`;
  });

  lifecycle.own(() => {
    video.pause();
    video.removeAttribute("src");
    video.load();
  });
  return { element: el, dispose: () => lifecycle.dispose() };
}
