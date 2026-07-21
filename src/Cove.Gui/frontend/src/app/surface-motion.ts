export interface SurfaceMotion {
  open(): void;
  close(): void;
  dispose(): void;
}

export function createSurfaceMotion(root: HTMLElement, durationMs = 140): SurfaceMotion {
  let timer: number | null = null;
  let listening = false;

  const finish = (): void => {
    if (timer !== null) globalThis.clearTimeout(timer);
    timer = null;
    if (listening) root.removeEventListener("animationend", onAnimationEnd);
    listening = false;
    root.classList.remove("closing");
  };

  const onAnimationEnd = (event: AnimationEvent): void => {
    if (event.target === root) finish();
  };

  return {
    open(): void {
      finish();
      root.classList.add("open");
    },
    close(): void {
      if (!root.classList.contains("open")) return;
      root.classList.remove("open");
      root.classList.add("closing");
      listening = true;
      root.addEventListener("animationend", onAnimationEnd);
      timer = Number(globalThis.setTimeout(finish, durationMs));
    },
    dispose(): void {
      finish();
      root.classList.remove("open");
    },
  };
}
