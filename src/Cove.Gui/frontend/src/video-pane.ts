import { mediaUrl } from "./media-url";

export function renderVideoPane(filePath: string): HTMLElement {
  const el = document.createElement("div");
  el.className = "video-pane";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0d1117;color:#e6edf3;";

  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;border-bottom:1px solid #303030;display:flex;gap:8px;align-items:center;flex-shrink:0;font-size:13px;font-weight:600;";
  header.textContent = filePath.split("/").pop() || filePath;
  el.appendChild(header);

  const stage = document.createElement("div");
  stage.style.cssText = "flex:1;min-height:0;display:flex;align-items:center;justify-content:center;overflow:hidden;";
  const video = document.createElement("video");
  video.src = mediaUrl(filePath);
  video.controls = true;
  video.preload = "metadata";
  video.style.cssText = "max-width:100%;max-height:100%;outline:none;";
  stage.appendChild(video);
  el.appendChild(stage);

  return el;
}
