import { describe, it, expect } from "vitest";
import {
  ONBOARDING_STEPS,
  INITIAL_ONBOARDING_STATE,
  nextStep,
  prevStep,
  goToStep,
  dismiss,
  selectAdapter,
  setTelemetryOptIn,
  currentStepData,
  isLastStep,
  isFirstStep,
  progressPercent,
  shouldShowOnboarding,
} from "./onboarding";

describe("ONBOARDING_STEPS", () => {
  it("has 4 steps in correct order", () => {
    expect(ONBOARDING_STEPS.length).toBe(4);
    expect(ONBOARDING_STEPS[0].id).toBe("welcome");
    expect(ONBOARDING_STEPS[1].id).toBe("adapters");
    expect(ONBOARDING_STEPS[2].id).toBe("telemetry");
    expect(ONBOARDING_STEPS[3].id).toBe("ready");
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

describe("selectAdapter / setTelemetryOptIn", () => {
  it("sets the selected adapter", () => {
    const s = selectAdapter(INITIAL_ONBOARDING_STATE, "claude");
    expect(s.selectedAdapter).toBe("claude");
  });
  it("can clear adapter selection", () => {
    const s = selectAdapter({ ...INITIAL_ONBOARDING_STATE, selectedAdapter: "claude" }, null);
    expect(s.selectedAdapter).toBeNull();
  });
  it("sets telemetry opt-in", () => {
    const s = setTelemetryOptIn(INITIAL_ONBOARDING_STATE, true);
    expect(s.telemetryOptIn).toBe(true);
  });
});

describe("currentStepData", () => {
  it("returns the step data for the current index", () => {
    const s = { ...INITIAL_ONBOARDING_STATE, currentStep: 2 };
    expect(currentStepData(s).id).toBe("telemetry");
  });
  it("falls back to first step for out-of-range", () => {
    const s = { ...INITIAL_ONBOARDING_STATE, currentStep: 99 };
    expect(currentStepData(s).id).toBe("welcome");
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
  it("returns 25 on first step", () => {
    expect(progressPercent(INITIAL_ONBOARDING_STATE)).toBe(25);
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
