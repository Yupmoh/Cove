export interface OnboardingStep {
  id: string;
  title: string;
  body: string;
}

export const ONBOARDING_STEPS: ReadonlyArray<OnboardingStep> = [
  { id: "welcome", title: "Welcome to Cove", body: "A free, open-source, AI-native terminal workspace. Let's get you set up in a few steps." },
  { id: "adapters", title: "Choose your adapter", body: "Adapters connect Cove to your AI coding tools. Pick one to start, or skip and configure later in Settings." },
  { id: "telemetry", title: "Telemetry", body: "Cove collects anonymous usage data only if you opt in. No scrollback, file paths, or personal content is ever sent. You can change this anytime in Settings → Privacy." },
  { id: "ready", title: "You're all set", body: "Press Cmd+T to create a new room, Cmd+, for settings, or Cmd+Shift+P for the command palette." },
];

export interface OnboardingState {
  currentStep: number;
  completed: boolean;
  dismissed: boolean;
  selectedAdapter: string | null;
  telemetryOptIn: boolean;
}

export const INITIAL_ONBOARDING_STATE: OnboardingState = {
  currentStep: 0,
  completed: false,
  dismissed: false,
  selectedAdapter: null,
  telemetryOptIn: false,
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

export function selectAdapter(state: OnboardingState, adapter: string | null): OnboardingState {
  return { ...state, selectedAdapter: adapter };
}

export function setTelemetryOptIn(state: OnboardingState, optIn: boolean): OnboardingState {
  return { ...state, telemetryOptIn: optIn };
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
