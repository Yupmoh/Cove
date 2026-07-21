import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const overlay = readFileSync(new URL("./features/settings/settings-overlay.css", import.meta.url), "utf8");
const settings = readFileSync(new URL("./features/settings/settings.css", import.meta.url), "utf8");
const launcher = readFileSync(new URL("./features/launcher/box-launcher.css", import.meta.url), "utf8");
const files = [overlay, settings, launcher];

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

  it("rejects unsafe resets, layers, properties, and long finite durations", () => {
    for (const source of files) {
      expect(source).not.toMatch(/(^|[,{}])\s*\*\s*[{,]/m);
      expect(source).not.toMatch(/transition:\s*none/);
      expect(source).not.toMatch(/will-change\s*:/);
      expect(source).not.toMatch(/(?:animation|transition)(?:-[a-z]+)?\s*:[^;}]*\b(?:width|height|inset|margin|padding|grid|flex|box-shadow|filter|backdrop-filter|scroll)/);
      expect(source).not.toMatch(/(?:animation-library|framer-motion|motion-one)/);
    }
    const withoutContinuousTip = launcher.replace(/\.cl-tip\.driving \.cl-tip-text\s*\{[^}]+\}/, "");
    for (const source of [overlay, settings, withoutContinuousTip]) {
      const durations = [...source.matchAll(/\b(\d*\.?\d+)(ms|s)\b/g)].map((match) => match[2] === "s" ? Number(match[1]) * 1000 : Number(match[1]));
      expect(durations.every((duration) => duration <= 200)).toBe(true);
    }
  });
});
