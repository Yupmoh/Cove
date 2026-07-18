export interface ComponentHandle {
  dispose(): void | Promise<void>;
}

export interface CoveComponent<Context> {
  mount(host: HTMLElement, context: Context): ComponentHandle;
}

type Disposer = () => void | Promise<void>;

export class LifecycleScope implements ComponentHandle {
  private readonly disposers: Disposer[] = [];
  private disposed = false;
  private disposal: Promise<void> | null = null;

  get isDisposed(): boolean {
    return this.disposed;
  }

  own(disposer: Disposer): void {
    if (this.disposed) {
      Promise.resolve(disposer()).catch((error: unknown) => {
        console.warn("late lifecycle resource disposal failed", error);
      });
      return;
    }
    this.disposers.push(disposer);
  }

  listen(
    target: EventTarget,
    type: string,
    listener: EventListenerOrEventListenerObject,
    options?: boolean | AddEventListenerOptions,
  ): void {
    this.assertActive();
    target.addEventListener(type, listener, options);
    this.own(() => target.removeEventListener(type, listener, options));
  }

  timeout(callback: () => void, delayMs: number): void {
    this.assertActive();
    let active = true;
    const timer = globalThis.setTimeout(() => {
      active = false;
      callback();
    }, delayMs);
    this.own(() => {
      if (!active) return;
      active = false;
      globalThis.clearTimeout(timer);
    });
  }

  interval(callback: () => void, delayMs: number): void {
    this.assertActive();
    const timer = globalThis.setInterval(callback, delayMs);
    this.own(() => globalThis.clearInterval(timer));
  }

  frame(callback: FrameRequestCallback): void {
    this.assertActive();
    const frame = globalThis.requestAnimationFrame(callback);
    this.own(() => globalThis.cancelAnimationFrame(frame));
  }

  dispose(): Promise<void> {
    if (this.disposal) return this.disposal;
    this.disposed = true;
    this.disposal = this.disposeOwned();
    return this.disposal;
  }

  private async disposeOwned(): Promise<void> {
    const errors: unknown[] = [];
    for (let index = this.disposers.length - 1; index >= 0; index -= 1) {
      try {
        await this.disposers[index]();
      } catch (error) {
        errors.push(error);
      }
    }
    this.disposers.length = 0;
    if (errors.length > 0) throw new AggregateError(errors, "lifecycle disposal failed");
  }

  private assertActive(): void {
    if (this.disposed) throw new Error("LifecycleScope is disposed");
  }
}
