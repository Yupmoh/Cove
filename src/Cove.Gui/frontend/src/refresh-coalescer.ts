export function createCoalescer(run: () => Promise<void>): () => Promise<void> {
  let inFlight = false;
  let queued = false;
  async function trigger(): Promise<void> {
    if (inFlight) {
      queued = true;
      return;
    }
    inFlight = true;
    try {
      await run();
    } finally {
      inFlight = false;
      if (queued) {
        queued = false;
        void trigger();
      }
    }
  }
  return trigger;
}
