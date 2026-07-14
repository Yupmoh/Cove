import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { spawn } from "node:child_process";
import { basename } from "node:path";

const cliPath = process.env.COVE_CLI_PATH ?? "";
const nookId = process.env.COVE_NOOK_ID ?? "";

function emit(event: string, payload: Record<string, unknown>): Promise<void> {
  if (!cliPath || !nookId) {
    console.error("cove omp hook missing COVE_CLI_PATH or COVE_NOOK_ID");
    return Promise.resolve();
  }

  const { promise, resolve } = Promise.withResolvers<void>();
  const child = spawn(
    cliPath,
    ["hook", "emit", event, "--adapter", "omp", "--nook-id", nookId],
    { stdio: ["pipe", "ignore", "pipe"] },
  );
  let stderr = "";
  child.stderr.setEncoding("utf8");
  child.stderr.on("data", (chunk: string) => {
    stderr += chunk;
  });
  child.on("error", (error) => {
    console.error(`cove omp hook failed event=${event} error=${error.message}`);
    resolve();
  });
  child.on("close", (code) => {
    if (code !== 0)
      console.error(`cove omp hook failed event=${event} exitCode=${code} stderr=${stderr.trim()}`);
    resolve();
  });
  child.stdin.end(JSON.stringify(payload));
  return promise;
}

function resolveSessionId(ctx: { sessionManager?: { getSessionFile?: () => string | null } }): string {
  const file = ctx.sessionManager?.getSessionFile?.();
  if (!file) {
    console.error("cove omp hook session file unavailable");
    return "";
  }

  const stem = basename(file).replace(/\.jsonl$/i, "");
  return stem.split("_").pop() ?? "";
}

export default function coveHooks(pi: ExtensionAPI): void {
  pi.on("session_start", async (_event, ctx) => {
    const sessionId = resolveSessionId(ctx as { sessionManager?: { getSessionFile?: () => string | null } });
    if (!sessionId) {
      console.error("cove omp hook session id unavailable");
      return;
    }
    await emit("session-start", { session_id: sessionId });
  });

  pi.on("session_shutdown", async () => {
    await emit("session-end", {});
  });
}
