import { invoke } from "./invoke";

export async function mediaUrl(filePath: string): Promise<string> {
  const result = await invoke<{ url: string }>("app.mediaLease", { filePath });
  return result.url;
}
