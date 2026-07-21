import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { createSettingsFeature, type SettingsFeatureDependencies } from "./settings-feature";

function fixture(withKeybindings = false, toolsAdapters: unknown[] | null = null, schemaEntries: unknown[] | null = null, renderDictationTab = vi.fn()) {
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
    if (command === "cove://commands/config.schema" && schemaEntries !== null) return { entries: schemaEntries } as T;
    if (command === "cove://commands/config.schema" && toolsAdapters !== null) {
      return { entries: [{ key: "tools.adapters", label: "Tools", tab: "tools", control: "text", description: null, type: "string", options: null }] } as T;
    }
    if (command === "cove://commands/adapter.tools-list" && toolsAdapters !== null) return { adapters: toolsAdapters } as T;
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
    renderDictationTab,
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
  return { window, root, tabs, body, close, focusActiveNook, invoke, clickBindingButton, feature };
}

describe("SettingsFeature", () => {
  it("renders launcher-aligned installed, available, and unavailable tool groups", async () => {
    const retention = { present: false, editable: false, hidden: false, value: null, recommended: null };
    const make = (name: string, status: string, installHint: string) => ({ name, displayName: name, accent: "#123456", binary: name, status, version: "1.0", binaryPath: `/bin/${name}`, iconSvg: null, installHint, bundled: true, removable: false, retention });
    const { body, feature } = fixture(false, [make("codex", "detected", ""), make("get-me", "missing", "install it"), make("blocked", "missing", "")]);

    feature.open("tools");
    await vi.waitFor(() => expect(body.querySelectorAll(".tools-card")).toHaveLength(3));
    expect([...body.querySelectorAll(".tools-section-title")].map((node) => node.textContent)).toEqual(["Installed · 1", "Available to install · 1", "Unavailable · 1"]);
    expect(body.querySelector(".tools-card")?.getAttribute("style")).toContain("--adapter-accent");
    expect(body.querySelector(".tools-icon .adapter-icon-mask")?.getAttribute("aria-hidden")).toBe("true");
    expect(body.querySelector(".tools-card")?.getAttribute("role")).toBe("article");
    expect(body.querySelector(".tools-actions button")?.textContent).toBe("Rescan");
  });

  it("keeps diagnostics utilities inside spaced setting groups", async () => {
    const { body, feature, invoke } = fixture();
    vi.mocked(invoke).mockImplementation(async (command) => {
      if (command === "cove://commands/config.schema") {
        return { entries: [{ key: "diagnostics.enabled", label: "Diagnostics", tab: "diagnostics", control: "boolean", description: null, type: "boolean", options: null }] } as never;
      }
      if (command === "cove://commands/config.get") return { value: false } as never;
      if (command === "cove://commands/perf.bundle.list") return { bundles: [] } as never;
      return {} as never;
    });

    feature.open("diagnostics");
    await vi.waitFor(() => expect(body.querySelector(".set-utility-input")).not.toBeNull());
    expect(body.querySelector(".set-utility-input")?.closest(".set-group")).not.toBeNull();
    expect(body.querySelector(".set-utility-actions")).not.toBeNull();
    expect(body.querySelector(".set-utility-note")?.closest(".set-group")).not.toBeNull();
  });

  it("keeps stale tool results from replacing a newly selected page", async () => {
    let complete!: (value: unknown) => void;
    const pending = new Promise((resolve) => { complete = resolve; });
    const { body, tabs, feature, invoke } = fixture(false, []);
    vi.mocked(invoke).mockImplementation(async (command) => command === "cove://commands/adapter.tools-list" ? await pending as never : ({ entries: [{ key: "tools.adapters", label: "Tools", tab: "tools", control: "text", description: null, type: "string", options: null }, { key: "theme.color", label: "Theme", tab: "theme", control: "text", description: null, type: "string", options: null }] }) as never);

    feature.open("tools");
    await vi.waitFor(() => expect(tabs.querySelector("#set-tab-theme")).not.toBeNull());
    (tabs.querySelector("#set-tab-theme") as unknown as HTMLButtonElement).click();
    complete({ adapters: [] });
    await Promise.resolve();
    expect(body.querySelector("#set-panel-theme")).not.toBeNull();
    expect(body.querySelector(".tools-empty")).toBeNull();
  });

  it("groups custom Settings renderers before displaying their content", async () => {
    const renderDictationTab = vi.fn((container: HTMLElement) => {
      const header = container.ownerDocument.createElement("div");
      header.className = "set-section-header";
      header.textContent = "Dictation controls";
      const row = container.ownerDocument.createElement("div");
      row.className = "set-row";
      container.append(header, row);
    });
    const schema = [{ key: "dictation.enabled", label: "Dictation", tab: "dictation", control: "boolean", description: null, type: "boolean", options: null }];
    const { body, feature } = fixture(false, null, schema, renderDictationTab);

    feature.open("dictation");
    await vi.waitFor(() => expect(renderDictationTab).toHaveBeenCalled());

    const content = body.querySelector(".set-page-content");
    expect(content?.children).toHaveLength(1);
    expect(content?.firstElementChild?.classList.contains("set-group")).toBe(true);
    expect(content?.querySelector(".set-row")?.closest(".set-group")).not.toBeNull();
  });

  it("uses shared Settings primitives without diagnostic or inline layout classes for Tools", () => {
    const source = createSettingsFeature.toString();
    expect(source).toContain("tools-section");
    expect(source).toContain("--adapter-accent");
    expect(source).not.toContain("style.gridTemplateColumns");
    expect(source).not.toMatch(/className = "(?:diag|perf)/);
  });
  it("renders grouped accessible tabs and a framed page with a stable auto-apply footer", async () => {
    const { window, tabs, body, feature } = fixture();

    feature.open("theme");
    await vi.waitFor(() => expect(tabs.querySelectorAll('[role="tab"]').length).toBeGreaterThan(0));
    expect(tabs.getAttribute("role")).toBe("tablist");
    expect(tabs.querySelectorAll(".set-nav-group-label").length).toBeGreaterThan(0);
    const selected = tabs.querySelector('[role="tab"][aria-selected="true"]') as unknown as HTMLElement | null;
    expect(selected?.tabIndex).toBe(0);
    expect(selected?.querySelector("svg")).not.toBeNull();
    expect(body.querySelectorAll(".set-page")).toHaveLength(1);
    expect(body.querySelector(".set-page-header")).not.toBeNull();
    expect(body.querySelector(".set-page-scroll")).not.toBeNull();
    expect(body.querySelector(".set-page-footer")).not.toBeNull();

    selected?.dispatchEvent(new window.KeyboardEvent("keydown", { key: "End", bubbles: true }) as unknown as Event);
    const allTabs = [...tabs.querySelectorAll('[role="tab"]')] as unknown as HTMLElement[];
    expect(allTabs[allTabs.length - 1].tabIndex).toBe(0);
  });

  it("uses classes instead of inline Settings layout and appearance styles", () => {
    const source = createSettingsFeature.toString();
    expect(source).not.toContain("style.cssText");
    expect(source).not.toContain('style="');
    expect(source).not.toMatch(/className = "(?:diag|perf)/);
  });

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
