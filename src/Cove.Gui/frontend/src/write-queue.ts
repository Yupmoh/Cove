export type NookWrite = (nookId: string, dataBase64: string) => Promise<unknown>;

const tails = new Map<string, Promise<void>>();

export function enqueueNookWrite(nookId: string, dataBase64: string, write: NookWrite): Promise<void> {
  const prev = tails.get(nookId) ?? Promise.resolve();
  const result = prev
    .then(() => write(nookId, dataBase64))
    .then(() => undefined)
    .catch((e) => {
      console.warn("nook write failed", nookId, e);
      throw e;
    });
  const tail = result.catch(() => undefined);
  tails.set(nookId, tail);
  void tail.then(() => {
    if (tails.get(nookId) === tail) tails.delete(nookId);
  });
  return result;
}

export function pendingNookWrites(): number {
  return tails.size;
}
