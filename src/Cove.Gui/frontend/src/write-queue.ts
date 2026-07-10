export type WriteFn = (paneId: string, dataBase64: string) => Promise<unknown>;

const tails = new Map<string, Promise<void>>();

export function enqueuePaneWrite(paneId: string, dataBase64: string, write: WriteFn): Promise<void> {
  const prev = tails.get(paneId) ?? Promise.resolve();
  const next = prev
    .then(() => write(paneId, dataBase64))
    .then(
      () => undefined,
      (e) => {
        console.warn("pane write failed", paneId, e);
      },
    );
  tails.set(paneId, next);
  void next.then(() => {
    if (tails.get(paneId) === next) tails.delete(paneId);
  });
  return next;
}

export function pendingPaneWrites(): number {
  return tails.size;
}
