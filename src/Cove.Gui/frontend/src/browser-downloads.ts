export type DownloadState = "prompt" | "inProgress" | "completed" | "failed";

export interface DownloadItem {
  downloadId: string;
  url: string;
  suggestedName: string;
  state: DownloadState;
  receivedBytes: number;
  totalBytes: number;
  path: string | null;
  error: string | null;
}

export function formatBytes(bytes: number): string {
  if (bytes <= 0) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"];
  const exp = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  const value = bytes / Math.pow(1024, exp);
  return `${exp === 0 ? value : value.toFixed(1)} ${units[exp]}`;
}

export function downloadPercent(item: DownloadItem): number | null {
  if (item.state === "completed") return 100;
  if (item.totalBytes > 0) return Math.min(100, Math.round((item.receivedBytes / item.totalBytes) * 100));
  return null;
}

export function joinPath(dir: string, name: string): string {
  if (dir.length === 0) return name;
  const sep = dir.endsWith("/") ? "" : "/";
  return `${dir}${sep}${name}`;
}

export class DownloadShelfState {
  private items = new Map<string, DownloadItem>();
  private order: string[] = [];

  requested(downloadId: string, url: string, suggestedName: string): DownloadItem {
    const item: DownloadItem = {
      downloadId,
      url,
      suggestedName,
      state: "prompt",
      receivedBytes: 0,
      totalBytes: 0,
      path: null,
      error: null,
    };
    if (!this.items.has(downloadId)) this.order.push(downloadId);
    this.items.set(downloadId, item);
    return item;
  }

  allow(downloadId: string, path: string): DownloadItem | null {
    const item = this.items.get(downloadId);
    if (!item) return null;
    item.state = "inProgress";
    item.path = path;
    return item;
  }

  deny(downloadId: string): void {
    if (this.items.delete(downloadId)) this.order = this.order.filter((id) => id !== downloadId);
  }

  progress(downloadId: string, receivedBytes: number, totalBytes: number): DownloadItem | null {
    const item = this.items.get(downloadId);
    if (!item || item.state === "prompt") return null;
    item.receivedBytes = receivedBytes;
    if (totalBytes > 0) item.totalBytes = totalBytes;
    item.state = "inProgress";
    return item;
  }

  completed(downloadId: string, path?: string): DownloadItem | null {
    const item = this.items.get(downloadId);
    if (!item) return null;
    item.state = "completed";
    if (path) item.path = path;
    if (item.totalBytes > 0) item.receivedBytes = item.totalBytes;
    return item;
  }

  failed(downloadId: string, error: string): DownloadItem | null {
    const item = this.items.get(downloadId);
    if (!item) return null;
    item.state = "failed";
    item.error = error;
    return item;
  }

  get(downloadId: string): DownloadItem | null {
    return this.items.get(downloadId) ?? null;
  }

  get shelf(): DownloadItem[] {
    return this.order
      .map((id) => this.items.get(id))
      .filter((i): i is DownloadItem => i !== undefined && i.state !== "prompt");
  }

  get prompts(): DownloadItem[] {
    return this.order
      .map((id) => this.items.get(id))
      .filter((i): i is DownloadItem => i !== undefined && i.state === "prompt");
  }
}
