export async function invoke<T>(cmd: string, args: unknown): Promise<T> {
  const result = await window.__ryn.invoke(cmd, args as Record<string, unknown>);
  return JSON.parse(result as string) as T;
}
