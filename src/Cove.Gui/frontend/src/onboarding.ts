export interface OnboardingStep {
  id: string;
  title: string;
  body: string;
}

export const ONBOARDING_STEPS: ReadonlyArray<OnboardingStep> = [
  { id: "harness", title: "Detect your tools", body: "Cove scans your login shell for installed AI coding CLIs. Pick a default bay directory to work in." },
  { id: "permissions", title: "Permission defaults", body: "Choose which adapters launch with bypass-permissions (YOLO) mode on by default. You can change this per session later." },
  { id: "appearance", title: "Appearance", body: "Set the window backdrop material and colour theme. These apply immediately and are saved to your settings." },
  { id: "sound", title: "Sound & notifications", body: "Agent chimes play a soft tone when an agent finishes or needs input. Toggle them and notifications here." },
];

export interface OnboardingState {
  currentStep: number;
  completed: boolean;
  dismissed: boolean;
  defaultBayDir: string | null;
  adapterYolo: Record<string, boolean>;
  backdrop: string;
  theme: string | null;
  agentChimes: boolean;
}

export const INITIAL_ONBOARDING_STATE: OnboardingState = {
  currentStep: 0,
  completed: false,
  dismissed: false,
  defaultBayDir: null,
  adapterYolo: {},
  backdrop: "none",
  theme: null,
  agentChimes: true,
};

export function nextStep(state: OnboardingState): OnboardingState {
  if (state.currentStep >= ONBOARDING_STEPS.length - 1) {
    return { ...state, completed: true, currentStep: ONBOARDING_STEPS.length - 1 };
  }
  return { ...state, currentStep: state.currentStep + 1 };
}

export function prevStep(state: OnboardingState): OnboardingState {
  return { ...state, currentStep: Math.max(0, state.currentStep - 1) };
}

export function goToStep(state: OnboardingState, step: number): OnboardingState {
  return { ...state, currentStep: Math.max(0, Math.min(ONBOARDING_STEPS.length - 1, step)) };
}

export function dismiss(state: OnboardingState): OnboardingState {
  return { ...state, dismissed: true, completed: true };
}

export function setDefaultBayDir(state: OnboardingState, dir: string | null): OnboardingState {
  return { ...state, defaultBayDir: dir };
}

export function setAdapterYolo(state: OnboardingState, adapter: string, on: boolean): OnboardingState {
  return { ...state, adapterYolo: { ...state.adapterYolo, [adapter]: on } };
}

export function setBackdrop(state: OnboardingState, backdrop: string): OnboardingState {
  return { ...state, backdrop };
}

export function setTheme(state: OnboardingState, theme: string | null): OnboardingState {
  return { ...state, theme };
}

export function setAgentChimes(state: OnboardingState, on: boolean): OnboardingState {
  return { ...state, agentChimes: on };
}

export function currentStepData(state: OnboardingState): OnboardingStep {
  return ONBOARDING_STEPS[state.currentStep] ?? ONBOARDING_STEPS[0];
}

export function isLastStep(state: OnboardingState): boolean {
  return state.currentStep === ONBOARDING_STEPS.length - 1;
}

export function isFirstStep(state: OnboardingState): boolean {
  return state.currentStep === 0;
}

export function progressPercent(state: OnboardingState): number {
  return Math.round(((state.currentStep + 1) / ONBOARDING_STEPS.length) * 100);
}

export function shouldShowOnboarding(hasSeenOnboarding: boolean): boolean {
  return !hasSeenOnboarding;
}

export const ONBOARDING_COMPLETED_KEY = "onboarding.completed";

export function onboardingSeenFromConfig(value: string | null | undefined): boolean {
  return (value ?? "").trim().toLowerCase() === "true";
}
