import type { FitAddon } from "@xterm/addon-fit";
import type { SearchAddon } from "@xterm/addon-search";
import type { SerializeAddon } from "@xterm/addon-serialize";
import type { ITerminalAddon, Terminal } from "@xterm/xterm";
import { isPaneFittable, scrollLineAfterFit, shouldResize, viewportScrollTopFor, type TermDims } from "./terminal-fit";
import { createKeyboardProtocolTracker, shiftEnterSequence, type KeyboardProtocolTracker } from "./terminal-keyboard";
import { decodeBase64Bytes, decodeRelayData, decodeTerminalRestoreBytes, parseRelayText, toBase64Utf8 } from "./wsproto";
import { processExitAction, replayViewportAction } from "./stream-guard";
import { FrontendCommand } from "./app/frontend-command";
import type { NookWrite } from "./write-queue";

const CREDIT_THRESHOLD = 131072;
const CHECKPOINT_QUIET_MS = 1000;
const CHECKPOINT_SAFETY_BYTES = 4 * 1024 * 1024;
type TimeoutHandle = ReturnType<typeof globalThis.setTimeout>;
type IntervalHandle = ReturnType<typeof globalThis.setInterval>;

export interface TerminalSettings {
  fontFamily: string;
  fontSize: number;
  lineHeight: number;
  letterSpacing: number;
  cursorStyle: "block" | "bar" | "underline";
  cursorBlink: boolean;
  scrollback: number;
  padding: number;
  backgroundOpacity: number;
}

export interface TerminalSessionResources {
  term: Terminal;
  fit: FitAddon;
  serialize: SerializeAddon;
  search: SearchAddon;
  renderer?: ITerminalAddon;
}

export interface TerminalSessionDependencies {
  createResources(): TerminalSessionResources;
  settings(): TerminalSettings;
  theme(): Record<string, string>;
  invoke<T>(command: FrontendCommand, args: unknown): Promise<T>;
  write: NookWrite;
  onExit(nookId: string): void;
  createSocket(url: string): WebSocket;
  createResizeObserver?(callback: ResizeObserverCallback): ResizeObserver;
  warn(message: string, context: unknown): void;
  onTitleChange?(title: string): void;
}

export class TerminalSession {
  readonly term: Terminal;
  readonly fit: FitAddon;
  readonly serialize: SerializeAddon;
  readonly search: SearchAddon;
  readonly keyboard: KeyboardProtocolTracker;
  socket: WebSocket | null = null;
  private receivedCounter: number;
  private committedCounter: number;
  private acknowledgedCounter: number;
  replayCursorOffset: number;
  replayTargetOffset: number;
  replaying = true;
  resetOnReplay: boolean;
  restoringCheckpoint = false;
  resizeObserver: ResizeObserver | null = null;
  fitFrame: number | null = null;
  reconnectTimer: TimeoutHandle | null = null;
  checkpointTimer: TimeoutHandle | null = null;
  exited = false;
  private ackTimer: IntervalHandle | null = null;
  private generation = 0;
  private controlEpoch = 0;
  private lastSent: TermDims | null = null;
  private handlersBound = false;
  private disposed = false;
  private savedViewport: { baseY: number; viewportY: number } | null = null;
  private replayCommittedCursorOffset: number;
  private replayAcknowledgedCursorOffset: number;
  private reconnectCursorOffset: number;
  private acceptedCheckpointOffset: number;
  private checkpointInFlight = false;
  private checkpointDirty = false;
  private checkpointRetryNotBefore = 0;
  private lastCheckpointActivityAt = 0;
  private pendingTerminalInput = 0;
  private lastTerminalInputAt = Number.NEGATIVE_INFINITY;
  private pendingTerminalWrites = 0;

  constructor(
    readonly nookId: string,
    since: number,
    readonly element: HTMLElement,
    host: HTMLElement,
    private readonly dependencies: TerminalSessionDependencies,
    resetOnReplay: boolean,
  ) {
    const resources = dependencies.createResources();
    this.term = resources.term;
    this.fit = resources.fit;
    this.serialize = resources.serialize;
    this.search = resources.search;
    this.term.loadAddon(this.fit);
    this.term.loadAddon(this.search);
    this.term.loadAddon(this.serialize);
    this.term.open(host);
    if (resources.renderer) {
      try {
        this.term.loadAddon(resources.renderer);
      } catch (error) {
        dependencies.warn("terminal canvas renderer unavailable", { nookId, error: String(error) });
      }
    }
    this.keyboard = createKeyboardProtocolTracker();
    this.receivedCounter = since;
    this.committedCounter = since;
    this.acknowledgedCounter = since;
    this.replayCursorOffset = since;
    this.replayCommittedCursorOffset = since;
    this.replayAcknowledgedCursorOffset = since;
    this.reconnectCursorOffset = since;
    this.acceptedCheckpointOffset = since;
    this.replayTargetOffset = since;
    this.resetOnReplay = resetOnReplay;
    this.observe(host);
    this.bindTerminalHandlers();
  }

  get connected(): boolean {
    return this.socket !== null && this.socket.readyState < WebSocket.CLOSING;
  }

  get receivedOffset(): number {
    return this.receivedCounter;
  }

  get committedOffset(): number {
    return this.committedCounter;
  }

  get acknowledgedOffset(): number {
    return this.acknowledgedCounter;
  }

  get socketClosed(): boolean {
    return this.socket === null || this.socket.readyState === WebSocket.CLOSED;
  }

  applySettings(): void {
    const settings = this.dependencies.settings();
    if (settings.fontFamily) this.term.options.fontFamily = settings.fontFamily;
    this.term.options.fontSize = settings.fontSize;
    this.term.options.lineHeight = settings.lineHeight;
    this.term.options.letterSpacing = settings.letterSpacing;
    this.term.options.cursorStyle = settings.cursorStyle;
    this.term.options.cursorBlink = settings.cursorBlink;
    this.term.options.scrollback = settings.scrollback;
    this.term.options.theme = this.dependencies.theme();
  }

  observe(host: HTMLElement): void {
    this.resizeObserver?.disconnect();
    this.resizeObserver = this.dependencies.createResizeObserver?.(() => this.scheduleFit())
      ?? new ResizeObserver(() => this.scheduleFit());
    this.resizeObserver.observe(host);
  }

  captureViewport(): void {
    if (!this.element.isConnected || this.replaying || this.restoringCheckpoint) return;
    this.savedViewport = {
      baseY: this.term.buffer.active.baseY,
      viewportY: this.term.buffer.active.viewportY,
    };
  }

  fitNow(): void {
    if (!this.isFittable()) return;
    try {
      const live = {
        baseY: this.term.buffer.active.baseY,
        viewportY: this.term.buffer.active.viewportY,
      };
      let before = live;
      if (!this.replaying && !this.restoringCheckpoint && this.savedViewport) {
        before = this.savedViewport;
        this.savedViewport = null;
      }
      this.fit.fit();
      const targetLine = scrollLineAfterFit(before, this.term.buffer.active.baseY);
      this.term.scrollToLine(targetLine);
      const viewport = this.element.querySelector<HTMLElement>(".xterm-viewport");
      if (viewport) {
        const scrollTop = viewportScrollTopFor(
          targetLine,
          this.term.buffer.active.baseY,
          viewport.scrollHeight,
          viewport.clientHeight,
        );
        if (scrollTop !== null) viewport.scrollTop = scrollTop;
      }
      this.term.refresh(0, Math.max(0, this.term.rows - 1));
    } catch {
      return;
    }
  }

  scheduleFit(): void {
    if (this.disposed || this.fitFrame !== null) return;
    this.fitFrame = requestAnimationFrame(() => {
      this.fitFrame = null;
      this.fitNow();
    });
  }

  connect(): void {
    if (this.disposed || this.exited || this.connected) return;
    if (this.reconnectTimer !== null) {
      globalThis.clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    const requestedSinceOffset = this.reconnectCursorOffset;
    const socket = this.dependencies.createSocket(
      `ws://${location.host}/pty?nook=${encodeURIComponent(this.nookId)}&since=${requestedSinceOffset}`,
    );
    this.socket = socket;
    this.replaying = true;
    this.replayTargetOffset = requestedSinceOffset;
    const generation = ++this.generation;
    const current = () => !this.disposed && this.generation === generation;
    socket.binaryType = "arraybuffer";
    const sendAck = () => {
      const offset = this.replayCommittedCursorOffset;
      if (socket.readyState === WebSocket.OPEN && offset > this.replayAcknowledgedCursorOffset) {
        try {
          socket.send(JSON.stringify({ t: "ack", off: offset }));
          this.replayAcknowledgedCursorOffset = offset;
          this.advanceAcknowledgedOffset(offset);
        } catch (error) {
          this.dependencies.warn("terminal acknowledgement failed", {
            nookId: this.nookId,
            offset,
            error: String(error),
          });
        }
      }
    };
    socket.onmessage = (event) => {
      if (!current()) return;
      if (typeof event.data === "string") {
        this.handleControlMessage(event.data, socket, current, requestedSinceOffset);
        return;
      }
      let frame;
      try {
        frame = decodeRelayData(event.data as ArrayBuffer);
      } catch (error) {
        this.dependencies.warn("invalid terminal stream frame", { nookId: this.nookId, error: String(error) });
        socket.close(1008, "invalid terminal stream frame");
        return;
      }
      const { offset, raw } = frame;
      const nextOffset = offset + raw.length;
      if (offset < this.replayCursorOffset && nextOffset <= this.replayCursorOffset) return;
      if (offset !== this.replayCursorOffset) {
        this.dependencies.warn("terminal stream offset mismatch", {
          nookId: this.nookId,
          expected: this.replayCursorOffset,
          received: offset,
        });
        socket.close(1008, "terminal stream offset mismatch");
        return;
      }
      this.replayCursorOffset = nextOffset;
      this.advanceReceivedOffset(nextOffset);
      this.keyboard.push(raw);
      const controlEpoch = this.controlEpoch;
      this.pendingTerminalWrites += 1;
      this.term.write(raw, () => {
        this.pendingTerminalWrites -= 1;
        if (!current() || this.controlEpoch !== controlEpoch) {
          if (this.pendingTerminalWrites === 0 && this.checkpointDirty) this.armCheckpointTimer();
          return;
        }
        this.replayCommittedCursorOffset = nextOffset;
        this.reconnectCursorOffset = nextOffset;
        this.advanceCommittedOffset(nextOffset);
        if (this.replayCommittedCursorOffset - this.replayAcknowledgedCursorOffset >= CREDIT_THRESHOLD) sendAck();
        this.scheduleCheckpoint();
        if (this.replaying && nextOffset >= this.replayTargetOffset) this.finishReplay();
      });
    };
    this.clearAckTimer();
    const ackTimer = globalThis.setInterval(() => {
      if (!current()) {
        this.clearAckTimer(ackTimer);
        return;
      }
      sendAck();
    }, 100);
    this.ackTimer = ackTimer;
    socket.onclose = () => {
      this.clearAckTimer(ackTimer);
      if (this.socket === socket) this.socket = null;
      if (!current() || this.exited || !this.element.isConnected) return;
      const reconnectTimer = globalThis.setTimeout(() => {
        if (!current() || this.reconnectTimer !== reconnectTimer) return;
        this.reconnectTimer = null;
        if (this.element.isConnected) this.connect();
      }, 250);
      this.reconnectTimer = reconnectTimer;
    };
    this.bindTerminalHandlers();
  }

  pause(): void {
    this.generation += 1;
    this.checkpointInFlight = false;
    this.checkpointDirty = false;
    this.checkpointRetryNotBefore = 0;
    this.clearCheckpointTimer();
    this.clearAckTimer();
    if (this.reconnectTimer !== null) {
      globalThis.clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    const socket = this.socket;
    this.socket = null;
    if (!socket) return;
    try {
      socket.close(1000, "terminal hidden");
    } catch (error) {
      this.dependencies.warn("terminal stream close failed", { nookId: this.nookId, error: String(error) });
    }
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.generation += 1;
    this.checkpointInFlight = false;
    this.checkpointDirty = false;
    this.clearAckTimer();
    if (this.reconnectTimer !== null) globalThis.clearTimeout(this.reconnectTimer);
    if (this.checkpointTimer !== null) globalThis.clearTimeout(this.checkpointTimer);
    if (this.fitFrame !== null) cancelAnimationFrame(this.fitFrame);
    this.reconnectTimer = null;
    this.checkpointTimer = null;
    this.fitFrame = null;
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
    const socket = this.socket;
    this.socket = null;
    if (socket) {
      try {
        socket.close();
      } catch (error) {
        this.dependencies.warn("terminal stream disposal failed", { nookId: this.nookId, error: String(error) });
      }
    }
    this.term.dispose();
  }

  private handleControlMessage(
    data: string,
    socket: WebSocket,
    current: () => boolean,
    requestedSinceOffset: number,
  ): void {
    let message: ReturnType<typeof parseRelayText>;
    try {
      message = parseRelayText(data);
    } catch (error) {
      this.dependencies.warn("invalid terminal control message", {
        nookId: this.nookId,
        error: String(error),
      });
      socket.close(1008, "invalid terminal control message");
      return;
    }
    if (!message) return;
    if (message.t === "base") {
      const hasCheckpoint = Boolean(message.checkpoint && message.checkpointCols && message.checkpointRows);
      const historyWasTrimmed = message.off > requestedSinceOffset && !hasCheckpoint;
      if (
        !this.isValidControlOffset(message.off)
        || !this.isValidControlOffset(message.head)
        || message.off < requestedSinceOffset
        || message.head < message.off
      ) {
        this.rejectControlOffset(socket, message.t, message.off, message.head);
        return;
      }
      const controlEpoch = ++this.controlEpoch;
      this.clearCheckpointTimer();
      this.beginReplayEpoch(message.off, message.head);
      this.restoringCheckpoint = false;
      if (hasCheckpoint) {
        this.term.reset();
        this.restoringCheckpoint = true;
        this.term.resize(message.checkpointCols!, message.checkpointRows!);
        this.term.write(decodeTerminalRestoreBytes(message.checkpoint!, message.modes), () => {
          if (!current() || this.controlEpoch !== controlEpoch) return;
          this.restoringCheckpoint = false;
          this.establishReplayBaseline(message.off);
          this.scheduleFit();
          if (message.head <= message.off) this.finishReplay();
        });
      } else {
        if ((this.resetOnReplay && this.replaying) || historyWasTrimmed) {
          this.term.reset();
          if (message.modes) {
            this.restoringCheckpoint = true;
            this.term.write(decodeBase64Bytes(message.modes), () => {
              if (!current() || this.controlEpoch !== controlEpoch) return;
              this.restoringCheckpoint = false;
              this.establishReplayBaseline(message.off);
              if (message.head <= message.off) this.finishReplay();
            });
            return;
          }
        }
        this.establishReplayBaseline(message.off);
        if (message.head <= message.off) this.finishReplay();
      }
      return;
    }
    if (message.t === "resync") {
      if (!this.isValidControlOffset(message.base)) {
        this.rejectControlOffset(socket, message.t, message.base);
        return;
      }
      const controlEpoch = ++this.controlEpoch;
      this.clearCheckpointTimer();
      this.term.reset();
      if (message.base < requestedSinceOffset) {
        this.generation += 1;
        this.receivedCounter = message.base;
        this.committedCounter = message.base;
        this.acknowledgedCounter = message.base;
        this.reconnectCursorOffset = message.base;
        this.acceptedCheckpointOffset = message.base;
        this.checkpointInFlight = false;
        this.checkpointDirty = false;
        this.checkpointRetryNotBefore = 0;
      }
      this.beginReplayEpoch(message.base, message.base);
      this.restoringCheckpoint = false;
      if (message.checkpoint && message.checkpointCols && message.checkpointRows) {
        this.restoringCheckpoint = true;
        this.term.resize(message.checkpointCols, message.checkpointRows);
        this.term.write(decodeTerminalRestoreBytes(message.checkpoint, message.modes), () => {
          if (!current() || this.controlEpoch !== controlEpoch) return;
          this.restoringCheckpoint = false;
          this.establishReplayBaseline(message.base);
          this.scheduleFit();
          this.finishReplay(true);
        });
      } else {
        if (message.modes) {
          this.restoringCheckpoint = true;
          this.term.write(decodeBase64Bytes(message.modes), () => {
            if (!current() || this.controlEpoch !== controlEpoch) return;
            this.restoringCheckpoint = false;
            this.establishReplayBaseline(message.base);
            this.finishReplay(true);
          });
          return;
        }
        this.establishReplayBaseline(message.base);
        this.finishReplay(true);
      }
      return;
    }
    if (message.t !== "end" || processExitAction(this.exited) === "ignore") return;
    this.exited = true;
    this.generation += 1;
    this.checkpointInFlight = false;
    this.checkpointDirty = false;
    this.clearCheckpointTimer();
    try {
      socket.close(1000, "process exited");
    } catch (error) {
      this.dependencies.warn("terminal process-exit close failed", { nookId: this.nookId, error: String(error) });
    }
    this.dependencies.onExit(this.nookId);
  }

  private finishReplay(resynced = false): void {
    const viewportAction = replayViewportAction({ resetOnReplay: this.resetOnReplay, resynced });
    this.replaying = false;
    this.resetOnReplay = false;
    if (viewportAction === "bottom") this.term.scrollToBottom();
    this.scheduleCheckpoint();
  }

  private scheduleCheckpoint(): void {
    if (this.disposed || this.exited) return;
    this.checkpointDirty = true;
    this.lastCheckpointActivityAt = Date.now();
    this.armCheckpointTimer();
  }

  private armCheckpointTimer(): void {
    this.clearCheckpointTimer();
    if (
      !this.checkpointDirty
      || this.checkpointInFlight
      || this.disposed
      || this.exited
    ) return;
    const now = Date.now();
    const safetyDue = this.replayCommittedCursorOffset - this.acceptedCheckpointOffset >= CHECKPOINT_SAFETY_BYTES;
    const outputDueAt = safetyDue ? now : this.lastCheckpointActivityAt + CHECKPOINT_QUIET_MS;
    const inputDueAt = this.lastTerminalInputAt + CHECKPOINT_QUIET_MS;
    const delay = Math.max(0, outputDueAt, inputDueAt, this.checkpointRetryNotBefore) - now;
    const checkpointTimer = globalThis.setTimeout(() => {
      if (this.checkpointTimer !== checkpointTimer) return;
      this.checkpointTimer = null;
      this.startCheckpoint();
    }, delay);
    this.checkpointTimer = checkpointTimer;
  }

  private startCheckpoint(): void {
    if (
      !this.checkpointDirty
      || this.checkpointInFlight
      || this.replaying
      || this.restoringCheckpoint
      || this.replayCursorOffset !== this.replayCommittedCursorOffset
      || this.receivedCounter !== this.committedCounter
      || this.pendingTerminalWrites !== 0
      || this.pendingTerminalInput !== 0
      || this.exited
      || this.disposed
    ) return;
    const now = Date.now();
    const safetyDue = this.replayCommittedCursorOffset - this.acceptedCheckpointOffset >= CHECKPOINT_SAFETY_BYTES;
    const inputDueAt = this.lastTerminalInputAt + CHECKPOINT_QUIET_MS;
    const outputDueAt = this.lastCheckpointActivityAt + CHECKPOINT_QUIET_MS;
    if (now < inputDueAt || (!safetyDue && now < outputDueAt)) {
      this.armCheckpointTimer();
      return;
    }
    const settings = this.dependencies.settings();
    const offset = this.replayCommittedCursorOffset;
    const cols = this.term.cols;
    const rows = this.term.rows;
    const scrollbackLines = settings.scrollback;
    const serializedVt = this.serialize.serialize();
    this.checkpointDirty = false;
    this.checkpointInFlight = true;
    const generation = this.generation;
    const controlEpoch = this.controlEpoch;
    let request: Promise<unknown>;
    try {
      request = this.dependencies.invoke(FrontendCommand.AppNookCheckpoint, {
        nookId: this.nookId,
        serializedVt,
        offset,
        cols,
        rows,
        scrollbackLines,
      });
    } catch (error) {
      this.settleCheckpoint(offset, false, error, generation, controlEpoch);
      return;
    }
    void request.then(
      () => this.settleCheckpoint(offset, true, undefined, generation, controlEpoch),
      (error: unknown) => this.settleCheckpoint(offset, false, error, generation, controlEpoch),
    );
  }

  private settleCheckpoint(offset: number, accepted: boolean, error?: unknown, generation = this.generation, controlEpoch = this.controlEpoch): void {
    if (this.disposed || this.exited || generation !== this.generation || controlEpoch !== this.controlEpoch) return;
    this.checkpointInFlight = false;
    if (accepted) {
      this.acceptedCheckpointOffset = Math.max(this.acceptedCheckpointOffset, offset);
      this.checkpointRetryNotBefore = 0;
    } else {
      const context = typeof error === "object" && error !== null ? error as { code?: unknown; message?: unknown } : null;
      const code = typeof context?.code === "string" ? context.code : "";
      const message = typeof context?.message === "string" ? context.message : String(error);
      const permanent = code === "not_found" || code === "invalid_params" || /disposed|disconnected|payload rejected/i.test(message);
      this.checkpointDirty = !permanent;
      if (!permanent) this.checkpointRetryNotBefore = Date.now() + 1000;
      this.dependencies.warn("terminal checkpoint failed", { nookId: this.nookId, offset, code, error: message, permanent });
    }
    if (this.checkpointDirty) this.armCheckpointTimer();
  }

  private writeTerminalInput(data: string): void {
    this.pendingTerminalInput += 1;
    this.lastTerminalInputAt = Date.now();
    this.clearCheckpointTimer();
    let request: Promise<unknown>;
    try {
      request = this.dependencies.write(this.nookId, toBase64Utf8(data));
    } catch {
      this.settleTerminalInput();
      return;
    }
    void request.then(
      () => this.settleTerminalInput(),
      () => this.settleTerminalInput(),
    );
  }

  private settleTerminalInput(): void {
    this.pendingTerminalInput -= 1;
    this.lastTerminalInputAt = Date.now();
    if (this.pendingTerminalInput === 0 && this.checkpointDirty) this.armCheckpointTimer();
  }

  private bindTerminalHandlers(): void {
    if (this.handlersBound) return;
    this.handlersBound = true;
    this.term.onData((data) => {
      if (this.replaying) return;
      this.writeTerminalInput(data);
    });
    this.term.onResize(({ cols, rows }) => {
      if (this.restoringCheckpoint) return;
      const dimensions: TermDims = { cols, rows };
      if (!shouldResize(dimensions, this.lastSent, this.isFittable())) return;
      this.lastSent = dimensions;
      void this.dependencies.invoke(FrontendCommand.AppNookResize, { nookId: this.nookId, cols, rows });
    });
    this.term.onTitleChange((title) => this.dependencies.onTitleChange?.(title));
    this.term.attachCustomKeyEventHandler((event) => {
      if (event.shiftKey && event.key === "Enter" && event.type !== "keydown") return false;
      if (event.type !== "keydown") return true;
      if (event.key === "Tab") {
        event.preventDefault();
        return true;
      }
      if (event.shiftKey && event.key === "Enter") {
        this.writeTerminalInput(shiftEnterSequence(this.keyboard.encoding()));
        return false;
      }
      if (!event.metaKey || event.altKey || event.ctrlKey) return true;
      const key = event.key.toLowerCase();
      if (key === "c") {
        if (this.term.hasSelection()) {
          const selection = this.term.getSelection();
          if (selection && navigator.clipboard) void navigator.clipboard.writeText(selection);
        } else {
          this.writeTerminalInput("\u0003");
        }
        return false;
      }
      if (key === "a") {
        this.term.selectAll();
        return false;
      }
      if (key === "v") {
        if (navigator.clipboard?.readText) {
          void navigator.clipboard.readText().then((text) => {
            if (text) this.writeTerminalInput(text);
          });
        }
        return false;
      }
      if (event.key === "ArrowLeft") {
        this.writeTerminalInput("\u0001");
        return false;
      }
      if (event.key === "ArrowRight") {
        this.writeTerminalInput("\u0005");
        return false;
      }
      if (event.key === "Backspace") {
        this.writeTerminalInput("\u0015");
        return false;
      }
      return true;
    });
  }

  private isFittable(): boolean {
    const host = this.term.element?.parentElement as HTMLElement | null;
    if (!host) return false;
    return isPaneFittable(host.clientWidth, host.clientHeight, host.isConnected, host.offsetParent !== null);
  }

  private clearAckTimer(timer: IntervalHandle | null = this.ackTimer): void {
    if (timer === null) return;
    globalThis.clearInterval(timer);
    if (this.ackTimer === timer) this.ackTimer = null;
  }

  private clearCheckpointTimer(): void {
    if (this.checkpointTimer === null) return;
    globalThis.clearTimeout(this.checkpointTimer);
    this.checkpointTimer = null;
  }

  private isValidControlOffset(value: unknown): value is number {
    return typeof value === "number" && Number.isSafeInteger(value) && value >= 0;
  }

  private rejectControlOffset(socket: WebSocket, type: "base" | "resync", offset: unknown, head?: unknown): void {
    this.dependencies.warn("invalid terminal control offset", {
      nookId: this.nookId,
      type,
      offset,
      head,
      expected: this.replayCursorOffset,
    });
    socket.close(1008, "invalid terminal control offset");
  }

  private beginReplayEpoch(baseOffset: number, targetOffset: number): void {
    this.replaying = true;
    this.replayCursorOffset = baseOffset;
    this.replayCommittedCursorOffset = baseOffset;
    this.replayAcknowledgedCursorOffset = baseOffset;
    this.replayTargetOffset = targetOffset;
    this.advanceReceivedOffset(baseOffset);
  }

  private establishReplayBaseline(offset: number): void {
    this.replayCommittedCursorOffset = offset;
    this.reconnectCursorOffset = offset;
    this.advanceCommittedOffset(offset);
    this.advanceAcknowledgedOffset(offset);
  }

  private advanceReceivedOffset(offset: number): void {
    this.receivedCounter = Math.max(this.receivedCounter, offset);
  }

  private advanceCommittedOffset(offset: number): void {
    this.committedCounter = Math.max(this.committedCounter, offset);
  }

  private advanceAcknowledgedOffset(offset: number): void {
    this.acknowledgedCounter = Math.max(this.acknowledgedCounter, offset);
  }

}
