import { describe, it, expect } from "vitest";
import * as onboarding from "./onboarding";
import {
  ONBOARDING_STEPS,
  INITIAL_ONBOARDING_STATE,
  nextStep,
  prevStep,
  goToStep,
  dismiss,
  setDefaultBayDir,
  setAdapterYolo,
  setBackdrop,
  setTheme,
  setAgentChimes,
  currentStepData,
  isLastStep,
  isFirstStep,
  progressPercent,
  shouldShowOnboarding,
  onboardingSeenFromConfig,
  ONBOARDING_COMPLETED_KEY,
} from "./onboarding";

interface PartitionAdapter {
  name: string;
  displayName: string;
  status?: string | null;
  installCommand?: string | null;
}

const partitionOnboardingAdapters = (onboarding as unknown as {
  partitionOnboardingAdapters(adapters: PartitionAdapter[]): {
    installed: PartitionAdapter[];
    installable: PartitionAdapter[];
  };
}).partitionOnboardingAdapters;

describe("ONBOARDING_STEPS", () => {
  it("has the 5 first-run wizard steps in spec order", () => {
    expect(ONBOARDING_STEPS.length).toBe(5);
    expect(ONBOARDING_STEPS[0].id).toBe("harness");
    expect(ONBOARDING_STEPS[1].id).toBe("permissions");
    expect(ONBOARDING_STEPS[2].id).toBe("appearance");
    expect(ONBOARDING_STEPS[3].id).toBe("sound");
    expect(ONBOARDING_STEPS[4].id).toBe("dictation");
  });
});

describe("nextStep", () => {
  it("advances to the next step", () => {
    const s = nextStep(INITIAL_ONBOARDING_STATE);
    expect(s.currentStep).toBe(1);
  });
  it("completes onboarding on the last step", () => {
    const last = { ...INITIAL_ONBOARDING_STATE, currentStep: ONBOARDING_STEPS.length - 1 };
    const s = nextStep(last);
    expect(s.completed).toBe(true);
    expect(s.currentStep).toBe(ONBOARDING_STEPS.length - 1);
  });
});

describe("prevStep", () => {
  it("goes back one step", () => {
    const s = { ...INITIAL_ONBOARDING_STATE, currentStep: 2 };
    expect(prevStep(s).currentStep).toBe(1);
  });
  it("clamps at 0", () => {
    expect(prevStep(INITIAL_ONBOARDING_STATE).currentStep).toBe(0);
  });
});

describe("goToStep", () => {
  it("jumps to a specific step", () => {
    expect(goToStep(INITIAL_ONBOARDING_STATE, 2).currentStep).toBe(2);
  });
  it("clamps to valid range", () => {
    expect(goToStep(INITIAL_ONBOARDING_STATE, -1).currentStep).toBe(0);
    expect(goToStep(INITIAL_ONBOARDING_STATE, 99).currentStep).toBe(ONBOARDING_STEPS.length - 1);
  });
});

describe("dismiss", () => {
  it("marks onboarding as dismissed and completed", () => {
    const s = dismiss(INITIAL_ONBOARDING_STATE);
    expect(s.dismissed).toBe(true);
    expect(s.completed).toBe(true);
  });
});

describe("wizard field setters", () => {
  it("sets the default bay directory", () => {
    expect(setDefaultBayDir(INITIAL_ONBOARDING_STATE, "/home/moh/proj").defaultBayDir).toBe("/home/moh/proj");
    expect(setDefaultBayDir({ ...INITIAL_ONBOARDING_STATE, defaultBayDir: "/x" }, null).defaultBayDir).toBeNull();
  });

  it("toggles per-adapter yolo without clobbering siblings", () => {
    const a = setAdapterYolo(INITIAL_ONBOARDING_STATE, "claude-code", true);
    const b = setAdapterYolo(a, "codex", false);
    expect(b.adapterYolo).toEqual({ "claude-code": true, "codex": false });
    const c = setAdapterYolo(b, "claude-code", false);
    expect(c.adapterYolo["claude-code"]).toBe(false);
    expect(c.adapterYolo["codex"]).toBe(false);
  });

  it("sets backdrop, theme and agent chimes", () => {
    expect(setBackdrop(INITIAL_ONBOARDING_STATE, "blur").backdrop).toBe("blur");
    expect(setTheme(INITIAL_ONBOARDING_STATE, "mocha").theme).toBe("mocha");
    expect(setAgentChimes(INITIAL_ONBOARDING_STATE, false).agentChimes).toBe(false);
  });
});

describe("currentStepData", () => {
  it("returns the step data for the current index", () => {
    const s = { ...INITIAL_ONBOARDING_STATE, currentStep: 2 };
    expect(currentStepData(s).id).toBe("appearance");
  });
  it("falls back to first step for out-of-range", () => {
    const s = { ...INITIAL_ONBOARDING_STATE, currentStep: 99 };
    expect(currentStepData(s).id).toBe("harness");
  });
});

describe("isLastStep / isFirstStep", () => {
  it("returns true on last step", () => {
    const s = { ...INITIAL_ONBOARDING_STATE, currentStep: ONBOARDING_STEPS.length - 1 };
    expect(isLastStep(s)).toBe(true);
    expect(isFirstStep(s)).toBe(false);
  });
  it("returns true on first step", () => {
    expect(isFirstStep(INITIAL_ONBOARDING_STATE)).toBe(true);
    expect(isLastStep(INITIAL_ONBOARDING_STATE)).toBe(false);
  });
});

describe("progressPercent", () => {
  it("returns 20 on first step", () => {
    expect(progressPercent(INITIAL_ONBOARDING_STATE)).toBe(20);
  });
  it("returns 100 on last step", () => {
    const s = { ...INITIAL_ONBOARDING_STATE, currentStep: ONBOARDING_STEPS.length - 1 };
    expect(progressPercent(s)).toBe(100);
  });
});

describe("shouldShowOnboarding", () => {
  it("shows when not seen", () => {
    expect(shouldShowOnboarding(false)).toBe(true);
  });
  it("hides when already seen", () => {
    expect(shouldShowOnboarding(true)).toBe(false);
  });
});

describe("onboardingSeenFromConfig", () => {
  it("treats a stored 'true' as seen", () => {
    expect(onboardingSeenFromConfig("true")).toBe(true);
  });
  it("is case and whitespace insensitive", () => {
    expect(onboardingSeenFromConfig(" TRUE ")).toBe(true);
  });
  it("treats a missing value as not seen", () => {
    expect(onboardingSeenFromConfig(null)).toBe(false);
    expect(onboardingSeenFromConfig(undefined)).toBe(false);
    expect(onboardingSeenFromConfig("")).toBe(false);
  });
  it("treats any non-true value as not seen", () => {
    expect(onboardingSeenFromConfig("false")).toBe(false);
    expect(onboardingSeenFromConfig("1")).toBe(false);
  });
  it("drives shouldShowOnboarding from the persisted flag", () => {
    expect(shouldShowOnboarding(onboardingSeenFromConfig("true"))).toBe(false);
    expect(shouldShowOnboarding(onboardingSeenFromConfig(null))).toBe(true);
  });
});

describe("ONBOARDING_COMPLETED_KEY", () => {
  it("is the config key onboarding state persists under", () => {
    expect(ONBOARDING_COMPLETED_KEY).toBe("onboarding.completed");
  });
});

describe("partitionOnboardingAdapters", () => {
  it("projects the mixed bundled catalog to the four detected tools", () => {
    const adapters: PartitionAdapter[] = [
      { name: "omp", displayName: "OMP", status: "detected", installCommand: "npm install omp" },
      { name: "claude-code", displayName: "Claude Code", status: "detected", installCommand: "npm install claude" },
      { name: "codex", displayName: "Codex", status: "detected", installCommand: "npm install codex" },
      { name: "pi", displayName: "Pi", status: "detected", installCommand: "npm install pi" },
      { name: "openclaw", displayName: "OpenClaw", status: "missing", installCommand: "npm install openclaw" },
      { name: "hermes", displayName: "Hermes", status: "missing", installCommand: "npm install hermes" },
      { name: "cursor-agent", displayName: "Cursor Agent", status: "missing", installCommand: "curl cursor" },
      { name: "opencode", displayName: "opencode", status: "missing", installCommand: "npm install opencode" },
    ];

    expect(partitionOnboardingAdapters(adapters).installed.map((adapter) => adapter.name)).toEqual([
      "omp",
      "claude-code",
      "codex",
      "pi",
    ]);
  });

  it("fails closed for every non-detected status and omits blank install commands", () => {
    const adapters: PartitionAdapter[] = [
      { name: "missing", displayName: "Missing", status: "missing", installCommand: " install missing " },
      { name: "broken", displayName: "Broken", status: "broken", installCommand: "install broken" },
      { name: "null", displayName: "Null", status: null, installCommand: "install null" },
      { name: "unknown", displayName: "Unknown", status: "unexpected", installCommand: "install unknown" },
      { name: "absent", displayName: "Absent", installCommand: "install absent" },
      { name: "blank", displayName: "Blank", status: "missing", installCommand: "   " },
    ];

    const result = partitionOnboardingAdapters(adapters);

    expect(result.installed).toEqual([]);
    expect(result.installable.map((adapter) => adapter.name)).toEqual(["absent", "broken", "missing", "null", "unknown"]);
    expect(result.installable.find((adapter) => adapter.name === "missing")?.installCommand).toBe("install missing");
  });

  it("keeps groups disjoint, lets detected win duplicates, and preserves ordering conventions", () => {
    const adapters: PartitionAdapter[] = [
      { name: "zeta", displayName: " Zeta ", status: "detected", installCommand: "install zeta" },
      { name: "shared", displayName: " Shared missing ", status: "missing", installCommand: " install shared " },
      { name: "beta", displayName: " beta ", status: "missing", installCommand: " install beta " },
      { name: "alpha", displayName: " Alpha ", status: "missing", installCommand: " install alpha " },
      { name: "shared", displayName: " Shared detected ", status: "detected", installCommand: "install shared" },
      { name: "first", displayName: " First ", status: "detected" },
    ];

    const result = partitionOnboardingAdapters(adapters);

    expect(result.installed.map((adapter) => adapter.name)).toEqual(["zeta", "shared", "first"]);
    expect(result.installed.map((adapter) => adapter.displayName)).toEqual(["Zeta", "Shared detected", "First"]);
    expect(result.installable.map((adapter) => adapter.displayName)).toEqual(["Alpha", "beta"]);
    expect(result.installable.every((adapter) => !result.installed.some((installed) => installed.name === adapter.name))).toBe(true);
  });
});
