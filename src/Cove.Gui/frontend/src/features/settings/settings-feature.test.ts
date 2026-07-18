import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { createSettingsFeature, type SettingsFeatureDependencies } from "./settings-feature";

function fixture(withKeybindings = false) {
  const window = new Window();
  const root = window.document.createElement("div");
  const tabs = window.document.createElement("div");
  const body = window.document.createElement("div");
  const close = window.document.createElement("button");
  close.id = "set-close";
  root.append(tabs, body, close);
  window.document.body.appendChild(root);
  const focusActiveNook = vi.fn();
  const invoke = vi.fn(async <T>(command: string) => {
    if (command === "cove://commands/config.schema" && withKeybindings) {
      return {
        entries: [{
          key: "keyboard.bindings",
          label: "Bindings",
          tab: "keyboard",
          control: "text",
          description: null,
          type: "string",
          options: null,
        }],
      } as T;
    }
    if (command === "cove://commands/keybind.list") {
      return {
        bindings: [{
          chord: "cmd+p",
          actionType: "app-command",
          action: "view.palette",
          description: "Command palette",
        }],
        conflicts: [],
      } as T;
    }
    if (command === "cove://commands/keybind.set") return { success: false } as T;
    return { entries: [] } as T;
  }) as SettingsFeatureDependencies["invoke"];
  const dependencies: SettingsFeatureDependencies = {
    document: window.document as unknown as Document,
    storage: window.localStorage as unknown as Storage,
    root: root as unknown as HTMLElement,
    tabs: tabs as unknown as HTMLElement,
    body: body as unknown as HTMLElement,
    grid: window.document.createElement("div") as unknown as HTMLElement,
    invoke,
    invokeNative: vi.fn(async <T>() => ({} as T)) as SettingsFeatureDependencies["invokeNative"],
    terminalSettings: {
      fontFamily: "monospace",
      fontSize: 13,
      lineHeight: 1.4,
      letterSpacing: 0,
      cursorStyle: "bar",
      cursorBlink: true,
      scrollback: 9000,
      padding: 8,
      backgroundOpacity: 0.9,
    },
    loadTerminalSettings: vi.fn(async () => ({})),
    applyTerminalSettings: vi.fn(),
    defaultTerminalTheme: {},
    themeBackgroundWithOpacity: () => "#000",

    applyTerminalTheme: vi.fn(),
    focusActiveNook,
    renderDictationTab: vi.fn(),
    rerunOnboarding: vi.fn(),
    renderUpdates: vi.fn(),
    setAgentChimesEnabled: vi.fn(),
    agentChimesEnabled: () => true,
    showToast: vi.fn(),
    launcherProfiles: () => new Map(),
    activeProjectDir: () => "/tmp",
    isPerfHudEnabled: () => false,
    togglePerfHud: vi.fn(),
    reloadKeymap: vi.fn(async () => {}),
    applyMarkdownSettings: vi.fn(),
  };
  const feature = createSettingsFeature(dependencies);
  const clickBindingButton = (): void => {
    body.querySelector("button")?.dispatchEvent(new window.MouseEvent("click", { bubbles: true }));
  };
  return { window, root, body, focusActiveNook, invoke, clickBindingButton, feature };
}

describe("SettingsFeature", () => {
  it("owns overlay open, close, and disposal listeners", async () => {
    const { root, focusActiveNook, feature } = fixture();

    feature.open();
    expect(root.classList.contains("open")).toBe(true);
    feature.close();
    expect(root.classList.contains("open")).toBe(false);
    expect(focusActiveNook).toHaveBeenCalledOnce();

    feature.open();
    root.dispatchEvent(new root.ownerDocument.defaultView!.KeyboardEvent("keydown", { key: "Escape" }));
    expect(root.classList.contains("open")).toBe(false);

    root.classList.add("open");
    await feature.dispose();
    root.classList.add("open");
    root.dispatchEvent(new root.ownerDocument.defaultView!.KeyboardEvent("keydown", { key: "Escape" }));
    expect(root.classList.contains("open")).toBe(true);
  });

  it("cancels keybinding recording on close and disposal", async () => {
    const { window, root, body, invoke, clickBindingButton, feature } = fixture(true);

    feature.open("keyboard");
    await vi.waitFor(() => expect(body.querySelector("button")?.textContent).toBe("⌘P"));
    clickBindingButton();

    feature.close();
    root.dispatchEvent(new window.KeyboardEvent("keydown", {
      key: "k",
      ctrlKey: true,
      shiftKey: true,
      bubbles: true,
      cancelable: true,
    }));
    expect(invoke).not.toHaveBeenCalledWith("cove://commands/keybind.set", expect.anything());

    feature.open("keyboard");
    await vi.waitFor(() => expect(body.querySelector("button")?.textContent).toBe("⌘P"));
    clickBindingButton();

    await feature.dispose();
    root.dispatchEvent(new window.KeyboardEvent("keydown", {
      key: "k",
      ctrlKey: true,
      shiftKey: true,
      bubbles: true,
      cancelable: true,
    }));
    expect(invoke).not.toHaveBeenCalledWith("cove://commands/keybind.set", expect.anything());
  });

  it("keeps exactly one recording listener across rerenders", async () => {
    const { window, root, body, invoke, clickBindingButton, feature } = fixture(true);

    feature.open("keyboard");
    await vi.waitFor(() => expect(body.querySelector("button")?.textContent).toBe("⌘P"));
    clickBindingButton();

    feature.render();
    await vi.waitFor(() => expect(body.querySelector("button")?.textContent).toBe("Press keys…"));
    feature.render();
    await vi.waitFor(() => expect(body.querySelector("button")?.textContent).toBe("Press keys…"));

    root.dispatchEvent(new window.KeyboardEvent("keydown", {
      key: "k",
      ctrlKey: true,
      shiftKey: true,
      bubbles: true,
      cancelable: true,
    }));
    expect(vi.mocked(invoke).mock.calls.filter(([command]) => command === "cove://commands/keybind.set")).toHaveLength(1);
    expect(invoke).toHaveBeenLastCalledWith("cove://commands/keybind.set", {
      chord: "ctrl+shift+k",
      actionType: "app-command",
      action: "view.palette",
    });

    await feature.dispose();
  });

  it("cancels recording with Escape without closing settings", async () => {
    const { window, root, body, invoke, clickBindingButton, feature } = fixture(true);

    feature.open("keyboard");
    await vi.waitFor(() => expect(body.querySelector("button")?.textContent).toBe("⌘P"));
    clickBindingButton();

    root.dispatchEvent(new window.KeyboardEvent("keydown", {
      key: "Escape",
      bubbles: true,
      cancelable: true,
    }));

    expect(root.classList.contains("open")).toBe(true);
    expect(body.querySelector("button")?.textContent).toBe("⌘P");
    expect(vi.mocked(invoke).mock.calls.filter(([command]) => command === "cove://commands/keybind.set")).toHaveLength(0);

    await feature.dispose();
  });
});
