import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const overlay = readFileSync(new URL("./features/settings/settings-overlay.css", import.meta.url), "utf8");
const settings = readFileSync(new URL("./features/settings/settings.css", import.meta.url), "utf8");
const launcher = readFileSync(new URL("./features/launcher/box-launcher.css", import.meta.url), "utf8");
const launcherOverlay = readFileSync(new URL("./features/launcher/launcher.css", import.meta.url), "utf8");
const visual = readFileSync(new URL("./workspace/visual-refinements.css", import.meta.url), "utf8");
const palette = readFileSync(new URL("./features/palette/palette.css", import.meta.url), "utf8");
const find = readFileSync(new URL("./features/find/find.css", import.meta.url), "utf8");
const onboarding = readFileSync(new URL("./features/onboarding/onboarding.css", import.meta.url), "utf8");
const contextMenu = readFileSync(new URL("./shell/context-menu.css", import.meta.url), "utf8");
const nookMenus = readFileSync(new URL("./workspace/nook-menus.css", import.meta.url), "utf8");
const splitChooser = readFileSync(new URL("./shell/split-chooser.css", import.meta.url), "utf8");
const sidebarWorkspaces = readFileSync(new URL("./workspace/sidebar-workspaces.css", import.meta.url), "utf8");
const workspaceCreate = readFileSync(new URL("./workspace/workspace-create.css", import.meta.url), "utf8");
const inspect = readFileSync(new URL("./features/inspect/inspect.css", import.meta.url), "utf8");
const dictation = readFileSync(new URL("./features/dictation/dictation.css", import.meta.url), "utf8");
const sidebar = readFileSync(new URL("./shell/sidebar.css", import.meta.url), "utf8");
const performanceHud = readFileSync(new URL("./features/performance/performance-hud.css", import.meta.url), "utf8");
const terminalScrollbars = readFileSync(new URL("./workspace/terminal-scrollbars.css", import.meta.url), "utf8");
const controls = readFileSync(new URL("./app/controls.css", import.meta.url), "utf8");
const nookChrome = readFileSync(new URL("./workspace/nook-chrome.css", import.meta.url), "utf8");
const notepad = readFileSync(new URL("./features/notepad/notepad.css", import.meta.url), "utf8");
const files = [overlay, settings, launcher, launcherOverlay, visual, palette, find, onboarding, contextMenu, nookMenus, splitChooser, sidebarWorkspaces, workspaceCreate, inspect, dictation, sidebar, performanceHud, terminalScrollbars, controls, nookChrome, notepad];

function rule(source: string, selector: string): string {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const match = source.match(new RegExp(`${escaped}\\s*\\{([^}]+)\\}`));
  expect(match, `missing ${selector}`).not.toBeNull();
  return match![1];
}

function reduced(source: string): string {
  const start = source.indexOf("@media (prefers-reduced-motion: reduce)");
  expect(start).toBeGreaterThanOrEqual(0);
  return source.slice(start);
}

describe("feature motion accessibility", () => {
  it("scopes short Settings entrances to opacity and transforms", () => {
    expect(rule(overlay, "#settings.open")).toContain("animation: set-backdrop-in 160ms ease-out");
    expect(rule(overlay, "#settings.open .set-box")).toContain("animation: set-panel-in 160ms ease-out");
    expect(rule(settings, ".set-page")).toContain("animation: set-page-in 140ms ease-out");
    expect(rule(overlay, "@keyframes set-backdrop-in")).toMatch(/opacity:\s*0/);
    expect(rule(overlay, "@keyframes set-panel-in")).toMatch(/opacity:\s*0;\s*transform:\s*translateY\(6px\)/);
    expect(rule(settings, "@keyframes set-page-in")).toMatch(/opacity:\s*0;\s*transform:\s*translateY\(4px\)/);
  });

  it("keeps Settings at one viewport size with isolated page scrolling", () => {
    expect(rule(settings, ".set-box")).toContain("height: min(720px, calc(100dvh - 32px))");
    expect(rule(settings, ".set-page-scroll")).toContain("overflow-y: auto");
  });

  it("gives Settings a visible staged page rhythm", () => {
    expect(rule(settings, ".set-page-header")).toContain("animation: set-header-in 160ms ease-out");
    expect(rule(settings, ".set-page-content > .set-group")).toContain("animation: set-group-in 180ms ease-out");
    expect(rule(settings, ".set-nav-item.active .set-nav-icon")).toContain("animation: set-nav-icon-pop 180ms ease-out");
    const settingsReduced = reduced(settings);
    for (const selector of [".set-page-header", ".set-page-content > .set-group", ".set-nav-item.active .set-nav-icon"]) {
      expect(settingsReduced).toContain(selector);
    }
  });

  it("covers Settings motion with feature-local reduction", () => {
    const overlayReduced = reduced(overlay);
    const settingsReduced = reduced(settings);
    expect(overlayReduced).toContain("#settings.open");
    expect(overlayReduced).toContain("#settings.open .set-box");
    expect(overlayReduced).toMatch(/animation:\s*none/);
    expect(overlayReduced).toMatch(/opacity:\s*1/);
    expect(overlayReduced).toMatch(/transform:\s*none/);
    for (const selector of [".set-page", ".set-nav-item", ".set-group", ".tools-card", ".set-action", ".set-key-control", ".set-utility-btn"]) {
      expect(settingsReduced).toContain(selector);
    }
    expect(settingsReduced).toMatch(/\.set-page[^}]*animation:\s*none[^}]*opacity:\s*1[^}]*transform:\s*none/s);
    expect(settingsReduced).toMatch(/\.set-group:hover[^}]*transform:\s*none/s);
    expect(settingsReduced).toMatch(/\.tools-card:hover[^}]*transform:\s*none/s);
    expect(settingsReduced).toMatch(/\.set-action:active[^}]*transform:\s*none/s);
    expect(settingsReduced).toContain(".set-nav-item.active");
    expect(settingsReduced).toContain(":focus-visible");
  });

  it("covers launcher movement while preserving state selectors", () => {
    const launcherReduced = reduced(launcher);
    for (const selector of [".cl-tip.driving .cl-tip-text", ".cl-dock", ".cl-resume-dd.open .cl-resume-menu", ".cl-card:hover", ".cl-tool:hover", ".cl-card-cta", ".cl-resume-dd.open .cl-resume-chev"]) {
      expect(launcherReduced).toContain(selector);
    }
    expect(launcherReduced).toMatch(/\.cl-tip\.driving \.cl-tip-text[^}]*animation:\s*none/s);
    expect(launcherReduced).toMatch(/\.cl-dock[^}]*animation:\s*none[^}]*transform:\s*none/s);
    expect(launcherReduced).toMatch(/\.cl-resume-dd\.open \.cl-resume-menu[^}]*animation:\s*none[^}]*transform:\s*none/s);
    expect(launcherReduced).toMatch(/\.cl-card:hover[^}]*transform:\s*none/s);
    expect(launcherReduced).toMatch(/\.cl-tool:hover[^}]*transform:\s*none/s);
    expect(launcherReduced).toMatch(/\.cl-card-cta[^}]*transform:\s*none/s);
    expect(launcherReduced).toMatch(/\.cl-resume-dd\.open \.cl-resume-chev[^}]*transform:\s*none/s);
    expect(launcherReduced).toContain(".cl-card.selected");
    expect(launcherReduced).toContain(".cl-tool.selected");
    expect(launcherReduced).toContain(".cl-resume-dd.open");
    expect(launcherReduced).toContain(":focus-visible");
  });

  it("stages launcher choices and responds to selection", () => {
    expect(rule(launcher, ".cl-card")).toContain("animation: cl-choice-in 180ms ease-out backwards");
    expect(rule(launcher, ".cl-tool")).toContain("animation: cl-choice-in 180ms ease-out backwards");
    expect(rule(launcher, ".cl-card.selected .cl-card-badge")).toContain("animation: cl-badge-select 180ms ease-out");
    const launcherReduced = reduced(launcher);
    expect(launcherReduced).toContain(".cl-card-badge");
    expect(launcherReduced).toContain("animation: none");
  });

  it("animates major transient surfaces and navigation without layout motion", () => {
    expect(rule(palette, "#palette.open .pal-box")).toContain("animation: cove-surface-pop-in 180ms ease-out");
    expect(rule(find, "#findbar.open")).toContain("animation: cove-surface-pop-in 150ms ease-out");
    expect(rule(onboarding, "#onboarding.open .ob-box")).toContain("animation: cove-surface-pop-in 190ms ease-out");
    expect(rule(contextMenu, ".ctx-menu")).toContain("animation: cove-surface-pop-in 130ms ease-out");
    expect(rule(nookMenus, ".nook-menu")).toContain("animation: cove-surface-pop-in 140ms ease-out");
    expect(rule(splitChooser, ".mini-launcher")).toContain("animation: cove-surface-pop-in 150ms ease-out");
    expect(rule(sidebarWorkspaces, ".ws-icon-popover")).toContain("animation: cove-surface-pop-in 140ms ease-out");
    expect(visual).not.toMatch(/\.rtab\s*\{[^}]*animation:/);
    expect(visual).not.toContain(".rtab:nth-child");
    expect(visual).not.toMatch(/\.ws-card\s*\{[^}]*animation:/);
    expect(visual).not.toContain(".ws-card:nth-child");
    expect(rule(workspaceCreate, "#ws-create.open")).toContain("animation: cove-fade-in 140ms ease-out");
    expect(rule(workspaceCreate, "#ws-create.open .wsc-box")).toContain("animation: cove-surface-pop-in 180ms ease-out");
    expect(rule(settings, ".settings-dialog-overlay")).toContain("animation: cove-fade-in 130ms ease-out");
    expect(rule(settings, ".settings-dialog, .settings-confirm-dialog")).toContain("animation: cove-surface-pop-in 160ms ease-out");
    expect(rule(inspect, ".inspect-note")).toContain("animation: cove-surface-pop-in 150ms ease-out");
    expect(rule(dictation, ".dictation-pill")).toContain("animation: dictation-pill-in 160ms ease-out");
    for (const source of [palette, find, onboarding, contextMenu, nookMenus, splitChooser, sidebarWorkspaces, visual]) {
      expect(reduced(source)).toMatch(/animation:\s*none/);
    }
    expect(visual).not.toMatch(/\.nook\.nook-opening\s*\{[^}]*animation:/);
    expect(rule(visual, ".nook.nook-opening .nook-header")).toContain("animation: nook-header-open-in 180ms ease-out");
    expect(rule(visual, ".nook.nook-opening .term-host")).toContain("animation: cove-fade-in 180ms ease-out");
    expect(rule(visual, ".nook.nook-opening::before")).toContain("animation: nook-open-reveal 200ms ease-out forwards");
    expect(rule(visual, ".nook.nook-repositioning")).toContain("animation: nook-reposition 180ms ease-out");
    expect(rule(terminalScrollbars, ".xterm-viewport")).toContain("pointer-events: auto");
    expect(rule(terminalScrollbars, ".xterm-viewport::-webkit-scrollbar")).toContain("width: 11px");
    const terminalThumb = rule(terminalScrollbars, ".xterm-viewport::-webkit-scrollbar-thumb");
    expect(terminalThumb).toContain("border: none");
    expect(terminalThumb).not.toContain("background-clip");
    expect(rule(sidebar, ".dual-sidebar:not(.collapsed) .sb-content")).toContain("animation: sidebar-open-in 180ms ease-out");
    expect(rule(performanceHud, "#perf-hud.open")).toContain("animation: cove-surface-pop-in 150ms ease-out");
    for (const source of [workspaceCreate, inspect, dictation, sidebar, performanceHud]) {
      expect(reduced(source)).toMatch(/animation:\s*none/);
    }
  });

  it("animates finite surface exits and cancels every exit under Reduced Motion", () => {
    expect(rule(overlay, "#settings.closing")).toContain("animation: set-backdrop-out 140ms ease-in forwards");
    expect(rule(launcherOverlay, "#launcher.closing")).toContain("animation: launcher-backdrop-out 140ms ease-in forwards");
    expect(rule(onboarding, "#onboarding.closing")).toContain("animation: cove-fade-out 140ms ease-in forwards");
    expect(rule(palette, "#palette.closing")).toContain("animation: cove-fade-out 140ms ease-in forwards");
    expect(rule(workspaceCreate, "#ws-create.closing")).toContain("animation: cove-fade-out 140ms ease-in forwards");
    for (const [source, selector] of [[overlay, "#settings.closing"], [launcherOverlay, "#launcher.closing"], [onboarding, "#onboarding.closing"], [palette, "#palette.closing"], [workspaceCreate, "#ws-create.closing"]] as const) {
      expect(reduced(source)).toContain(selector);
      expect(reduced(source)).toMatch(/animation:\s*none/);
    }
  });

  it("covers state, drop, control, loading, and empty feedback without layout animation", () => {
    expect(rule(visual, ".nook.agent-transition-running::after,\n.nook.agent-transition-needs-input::after,\n.nook.agent-transition-done::after,\n.nook.agent-transition-idle::after")).toContain("animation: nook-state-arrival 180ms ease-out forwards");
    expect(rule(nookChrome, ".drop-overlay-entering")).toContain("animation: drop-preview-in 150ms ease-out");
    expect(rule(nookChrome, ".nook.nook-drop-settled")).toContain("animation: nook-drop-settle 180ms ease-out");
    expect(rule(controls, "input[type=\"checkbox\"]:checked::after")).toContain("animation: control-check-in 140ms ease-out");
    expect(rule(sidebarWorkspaces, ".ws-icon-cell.sel svg, .ws-icon-cell.sel .ws-icon-none-dot")).toContain("animation: ws-icon-confirm 160ms ease-out");
    expect(rule(onboarding, ".ob-loading::before")).toContain("animation: ob-loading-sheen 1.1s linear infinite");
    expect(rule(palette, ".pal-no-results")).toContain("animation: pal-result-in 140ms ease-out");
    expect(rule(settings, ".tools-state-error, .tools-empty")).toContain("animation: tools-result-in 150ms ease-out");
    expect(rule(controls, ".cove-empty-state")).toContain("animation: cove-empty-in 150ms ease-out");
    expect(rule(notepad, ".ns-empty")).toContain("animation: ns-empty-in 150ms ease-out");
    for (const source of [visual, nookChrome, controls, sidebarWorkspaces, onboarding, palette, settings, notepad]) {
      expect(reduced(source)).toMatch(/animation:\s*none/);
    }
    expect(rule(nookChrome, "@keyframes nook-drop-settle")).not.toMatch(/\b(?:width|height|inset|margin|padding|grid|flex)\b/);
  });

  it("rejects unsafe resets, layers, properties, and long finite durations", () => {
    for (const source of files) {
      expect(source).not.toMatch(/(^|[,{}])\s*\*\s*[{,]/m);
      expect(source).not.toMatch(/transition:\s*none/);
      expect(source).not.toMatch(/will-change\s*:/);
      expect(source).not.toMatch(/(?:animation|transition)(?:-[a-z]+)?\s*:[^;}\n]*\b(?:width|height|inset|margin|padding|grid|flex|box-shadow|filter|backdrop-filter|scroll)/);
      expect(source).not.toMatch(/(?:animation-library|framer-motion|motion-one)/);
    }
    for (const source of [overlay, settings, launcher]) {
      const finiteMotion = source.replace(/animation:\s*[^;}\n]*\binfinite\b[^;}\n]*;?/g, "");
      const durations = [...finiteMotion.matchAll(/\b(\d*\.?\d+)(ms|s)\b/g)].map((match) => match[2] === "s" ? Number(match[1]) * 1000 : Number(match[1]));
      expect(durations.every((duration) => duration <= 200)).toBe(true);
    }
  });
});
