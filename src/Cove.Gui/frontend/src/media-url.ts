import { invoke } from "./invoke";
import { FrontendCommand } from "./app/frontend-command";

export async function mediaUrl(filePath: string): Promise<string> {
  const result = await invoke<{ url: string }>(FrontendCommand.AppMediaLease, { filePath });
  return result.url;
}
