import { describe, it, expect } from "vitest";
import {
  deriveProfileSlug,
  isValidProfileSlug,
  profileDisplayName,
  profilePickerLabel,
  modelChoicesWithFreeform,
  envRowsFromMap,
  envMapFromRows,
  cliArgsFromRows,
  firstDefault,
  type LaunchProfileListItem,
  type LauncherOption,
} from "./profiles";

const item = (over: Partial<LaunchProfileListItem> = {}): LaunchProfileListItem => ({
  slug: "glm-umans",
  name: "GLM Umans",
  adapter: "claude-code",
  isDefault: false,
  model: "glm",
  effort: "high",
  argCount: 1,
  envCount: 2,
  ...over,
});

describe("isValidProfileSlug", () => {
  it("accepts kebab-case slugs", () => {
    expect(isValidProfileSlug("glm-umans")).toBe(true);
    expect(isValidProfileSlug("a")).toBe(true);
    expect(isValidProfileSlug("opus-4-8")).toBe(true);
  });

  it("rejects invalid slugs", () => {
    expect(isValidProfileSlug("GLM Umans")).toBe(false);
    expect(isValidProfileSlug("")).toBe(false);
    expect(isValidProfileSlug("has space")).toBe(false);
    expect(isValidProfileSlug("under_score")).toBe(false);
  });
});

describe("deriveProfileSlug", () => {
  it("passes through already-valid slugs", () => {
    expect(deriveProfileSlug("ccx")).toBe("ccx");
    expect(deriveProfileSlug("glm-umans")).toBe("glm-umans");
  });

  it("slugifies names with case, spaces, and punctuation", () => {
    expect(deriveProfileSlug("Claude Code (CCX)")).toBe("claude-code-ccx");
    expect(deriveProfileSlug("  GLM Umans  ")).toBe("glm-umans");
    expect(deriveProfileSlug("under_score")).toBe("under-score");
  });

  it("returns empty for unusable input", () => {
    expect(deriveProfileSlug("")).toBe("");
    expect(deriveProfileSlug("   ")).toBe("");
    expect(deriveProfileSlug("()")).toBe("");
  });

  it("caps length at 64 without a trailing dash", () => {
    const derived = deriveProfileSlug("a".repeat(63) + " tail");
    expect(derived.length).toBeLessThanOrEqual(64);
    expect(derived.endsWith("-")).toBe(false);
    expect(isValidProfileSlug(derived)).toBe(true);
  });
});

describe("profileDisplayName / profilePickerLabel", () => {
  it("falls back to slug when name is empty", () => {
    expect(profileDisplayName(item({ name: "" }))).toBe("glm-umans");
  });

  it("prefixes the default marker and model", () => {
    const label = profilePickerLabel(item({ isDefault: true, model: "glm" }));
    expect(label).toBe("★ GLM Umans · glm");
  });

  it("omits model when null", () => {
    expect(profilePickerLabel(item({ isDefault: false, model: null }))).toBe("GLM Umans");
  });
});

describe("modelChoicesWithFreeform", () => {
  const option = (over: Partial<LauncherOption> = {}): LauncherOption => ({
    key: "model",
    label: "Model",
    type: "select",
    defaultValueRaw: "default",
    choices: [
      { value: "default", label: "Default" },
      { value: "opus", label: "Opus" },
    ],
    ...over,
  });

  it("returns base choices when current value is already a choice", () => {
    const result = modelChoicesWithFreeform(option().choices, "opus");
    expect(result).toHaveLength(2);
  });

  it("appends a freeform current value not in the choices", () => {
    const result = modelChoicesWithFreeform(option().choices, "glm");
    expect(result).toHaveLength(3);
    expect(result[2]).toEqual({ value: "glm", label: "glm" });
  });

  it("returns base choices when no current value", () => {
    const result = modelChoicesWithFreeform(option().choices, null);
    expect(result).toHaveLength(2);
  });
});

describe("env rows", () => {
  it("round-trips env map through rows and back", () => {
    const env = { ANTHROPIC_BASE_URL: "https://umans.ai", ANTHROPIC_API_KEY: "sk-1" };
    const rows = envRowsFromMap(env);
    expect(rows).toHaveLength(2);
    expect(envMapFromRows(rows)).toEqual(env);
  });

  it("drops rows with empty keys", () => {
    const rows = [
      { key: "ANTHROPIC_BASE_URL", value: "https://umans.ai" },
      { key: "  ", value: "ignored" },
    ];
    expect(envMapFromRows(rows)).toEqual({ ANTHROPIC_BASE_URL: "https://umans.ai" });
  });
});

describe("cliArgsFromRows", () => {
  it("joins flag and value with =", () => {
    const rows = [
      { flag: "--settings", value: "/path/to/settings.json" },
      { flag: "--dangerously-skip-permissions", value: null },
      { flag: "  ", value: null },
    ];
    expect(cliArgsFromRows(rows)).toEqual([
      "--settings=/path/to/settings.json",
      "--dangerously-skip-permissions",
    ]);
  });
});

describe("firstDefault", () => {
  it("returns the default profile", () => {
    const profiles = [item({ slug: "a", isDefault: false }), item({ slug: "b", isDefault: true })];
    expect(firstDefault(profiles)?.slug).toBe("b");
  });

  it("falls back to the first profile when none is default", () => {
    const profiles = [item({ slug: "a", isDefault: false }), item({ slug: "b", isDefault: false })];
    expect(firstDefault(profiles)?.slug).toBe("a");
  });

  it("returns null for an empty list", () => {
    expect(firstDefault([])).toBeNull();
  });
});
