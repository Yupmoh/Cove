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
  selectedLauncherProfile,
  launcherProfileChoices,
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
describe("selectedLauncherProfile", () => {
  const profiles = [
    item({ slug: "default", name: "Default" }),
    item({ slug: "ccx", name: "Claude Code (ccx)", isDefault: true }),
    item({ slug: "glm-umans", name: "GLM Umans" }),
  ];

  it("prefers the stored slug when it still exists", () => {
    expect(selectedLauncherProfile(profiles, "glm-umans")?.slug).toBe("glm-umans");
  });

  it("falls back to the default profile when the stored slug is gone", () => {
    expect(selectedLauncherProfile(profiles, "deleted")?.slug).toBe("ccx");
  });

  it("falls back to the default profile without a stored slug", () => {
    expect(selectedLauncherProfile(profiles, null)?.slug).toBe("ccx");
  });

  it("falls back to the first profile when none is default", () => {
    const noDefault = [item({ slug: "a" }), item({ slug: "b" })];
    expect(selectedLauncherProfile(noDefault, null)?.slug).toBe("a");
  });

  it("returns null for an empty list", () => {
    expect(selectedLauncherProfile([], "any")).toBeNull();
  });
});

describe("launcherProfileChoices", () => {
  it("prepends a stock Default when no profile uses the default slug", () => {
    const choices = launcherProfileChoices("claude-code", [item({ slug: "ccx", isDefault: true })]);
    expect(choices.map((p) => p.slug)).toEqual(["default", "ccx"]);
    expect(choices[0].name).toBe("Default");
    expect(choices[0].adapter).toBe("claude-code");
  });

  it("marks the stock Default as default only when no real default exists", () => {
    expect(launcherProfileChoices("claude-code", [item({ slug: "a" })])[0].isDefault).toBe(true);
    expect(launcherProfileChoices("claude-code", [item({ slug: "a", isDefault: true })])[0].isDefault).toBe(false);
  });

  it("does not duplicate an explicit default-slug profile", () => {
    const explicit = [item({ slug: "default", name: "Mine" }), item({ slug: "ccx" })];
    expect(launcherProfileChoices("claude-code", explicit)).toBe(explicit);
  });

  it("yields only the stock Default for an empty list", () => {
    const choices = launcherProfileChoices("codex", []);
    expect(choices).toHaveLength(1);
    expect(choices[0]).toMatchObject({ slug: "default", isDefault: true, adapter: "codex" });
  });

  it("keeps stored stock selection distinct from a real default profile", () => {
    const choices = launcherProfileChoices("claude-code", [item({ slug: "ccx", isDefault: true })]);
    expect(selectedLauncherProfile(choices, "default")?.slug).toBe("default");
    expect(selectedLauncherProfile(choices, null)?.slug).toBe("ccx");
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
