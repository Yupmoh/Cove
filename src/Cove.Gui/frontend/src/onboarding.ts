export interface OnboardingStep {
  id: string;
  title: string;
  body: string;
}

export interface OnboardingAdapter {
  name: string;
  displayName: string;
  status?: string | null;
  installCommand?: string | null;
}

export interface OnboardingAdapterPartition<T extends OnboardingAdapter> {
  installed: T[];
  installable: T[];
}

export function partitionOnboardingAdapters<T extends OnboardingAdapter>(adapters: readonly T[]): OnboardingAdapterPartition<T> {
  const detectedNames = new Set(adapters.filter((adapter) => adapter.status === "detected").map((adapter) => adapter.name));
  const installedNames = new Set<string>();
  const installableNames = new Set<string>();
  const installed: T[] = [];
  const installable: T[] = [];

  for (const adapter of adapters) {
    if (adapter.status !== "detected" || installedNames.has(adapter.name)) continue;
    installedNames.add(adapter.name);
    installed.push({
      ...adapter,
      displayName: adapter.displayName.trim(),
      installCommand: adapter.installCommand?.trim() ?? null,
    });
  }

  for (const adapter of adapters) {
    const installCommand = adapter.installCommand?.trim() ?? "";
    if (detectedNames.has(adapter.name) || installableNames.has(adapter.name) || installCommand.length === 0) continue;
    installableNames.add(adapter.name);
    installable.push({ ...adapter, displayName: adapter.displayName.trim(), installCommand });
  }

  installable.sort((left, right) => (left.displayName || left.name).localeCompare(right.displayName || right.name));
  return { installed, installable };
}

export const ONBOARDING_STEPS: ReadonlyArray<OnboardingStep> = [
  { id: "harness", title: "Set up Cove", body: "Review your coding tools and choose where new bays start." },
  { id: "permissions", title: "Permission defaults", body: "Choose which adapters launch with bypass-permissions (YOLO) mode on by default. You can change this per session later." },
  { id: "appearance", title: "Appearance", body: "Set the window backdrop material and colour theme. These apply immediately and are saved to your settings." },
  { id: "sound", title: "Sound & notifications", body: "Agent chimes play a soft tone when an agent finishes or needs input. Toggle them and notifications here." },
  { id: "dictation", title: "Voice dictation", body: "Hold F9 — or hold Space in a terminal or text field — to dictate. Speech is recognized entirely on this machine; audio never leaves it. The model downloads on first use, or grab it now." },
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
