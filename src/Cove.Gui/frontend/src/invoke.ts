export async function invoke<T>(cmd: string, args: unknown): Promise<T> {
  let result: unknown;
  if (cmd.startsWith("cove://")) {
    result = await window.__ryn.invoke("app.callEngine", { uri: cmd, argsJson: JSON.stringify(args ?? {}) });
  } else {
    result = await window.__ryn.invoke(cmd, args as Record<string, unknown>);
  }
  return JSON.parse(result as string) as T;
}
