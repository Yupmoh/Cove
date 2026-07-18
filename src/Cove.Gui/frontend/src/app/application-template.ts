import { mountFindBarTemplate } from "../features/find/find-bar-template";
import { mountLauncherTemplate } from "../features/launcher/launcher-template";
import { mountOnboardingTemplate } from "../features/onboarding/onboarding-template";
import {
  mountPaletteTemplate,
  mountPerformanceHudTemplate,
  mountSettingsTemplate,
  mountTitlebarTemplate,
  mountWorkspaceCreationTemplate,
  mountWorkspaceTemplate,
} from "../shell/app-shell-template";

export function mountApplicationTemplate(document: Document, root: HTMLElement): void {
  root.replaceChildren(
    mountTitlebarTemplate(document),
    mountWorkspaceTemplate(document),
    mountPaletteTemplate(document),
    mountOnboardingTemplate(document),
    mountSettingsTemplate(document),
    mountWorkspaceCreationTemplate(document),
    mountFindBarTemplate(document),
    mountLauncherTemplate(document),
    mountPerformanceHudTemplate(document),
  );
}
