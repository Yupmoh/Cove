export type WriteFn = (nookId: string, dataBase64: string) => Promise<unknown>;

const tails = new Map<string, Promise<void>>();

export function enqueueNookWrite(nookId: string, dataBase64: string, write: WriteFn): Promise<void> {
  const prev = tails.get(nookId) ?? Promise.resolve();
  const next = prev
    .then(() => write(nookId, dataBase64))
    .then(
      () => undefined,
      (e) => {
        console.warn("nook write failed", nookId, e);
      },
    );
  tails.set(nookId, next);
  void next.then(() => {
    if (tails.get(nookId) === next) tails.delete(nookId);
  });
  return next;
}

export function pendingNookWrites(): number {
  return tails.size;
}
