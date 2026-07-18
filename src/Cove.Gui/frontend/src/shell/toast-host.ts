export interface ToastAction {
  label: string;
  primary?: boolean;
  onClick: () => void;
}

export interface ToastOptions {
  actions?: ToastAction[];
  timeoutMs?: number;
}

export class ToastHost {
  private readonly timers = new Set<ReturnType<typeof globalThis.setTimeout>>();
  private readonly host: HTMLElement;
  private disposed = false;

  constructor(private readonly document: Document) {
    const existing = document.getElementById("toast-host");
    this.host = existing ?? document.createElement("div");
    this.host.id = "toast-host";
    if (!existing) document.body.appendChild(this.host);
  }

  show(title: string, body: string, onClick: () => void, options?: ToastOptions): void {
    if (this.disposed) throw new Error("ToastHost is disposed");
    const toast = this.document.createElement("div");
    toast.className = "toast";
    toast.setAttribute("role", "status");
    const titleElement = this.document.createElement("div");
    titleElement.className = "toast-title";
    titleElement.textContent = title;
    toast.appendChild(titleElement);
    if (body) {
      const bodyElement = this.document.createElement("div");
      bodyElement.className = "toast-body";
      bodyElement.textContent = body;
      toast.appendChild(bodyElement);
    }
    let dismissed = false;
    const dismiss = () => {
      if (dismissed) return;
      dismissed = true;
      toast.classList.add("leaving");
      this.schedule(() => toast.remove(), 200);
    };
    if (options?.actions?.length) {
      const row = this.document.createElement("div");
      row.className = "toast-actions";
      for (const action of options.actions) {
        const button = this.document.createElement("button");
        button.className = action.primary ? "toast-btn primary" : "toast-btn";
        button.textContent = action.label;
        button.addEventListener("click", (event) => {
          event.stopPropagation();
          action.onClick();
          dismiss();
        });
        row.appendChild(button);
      }
      toast.appendChild(row);
    }
    toast.addEventListener("click", () => {
      onClick();
      dismiss();
    });
    this.host.appendChild(toast);
    const animate = () => toast.classList.add("in");
    const view = this.document.defaultView;
    if (view) view.requestAnimationFrame(animate);
    else animate();
    this.schedule(dismiss, options?.timeoutMs ?? 6000);
  }

  async dispose(): Promise<void> {
    if (this.disposed) return;
    this.disposed = true;
    for (const timer of this.timers) globalThis.clearTimeout(timer);
    this.timers.clear();
    this.host.remove();
  }

  private schedule(callback: () => void, delayMs: number): void {
    const timer = globalThis.setTimeout(() => {
      this.timers.delete(timer);
      if (!this.disposed) callback();
    }, delayMs);
    this.timers.add(timer);
  }
}
