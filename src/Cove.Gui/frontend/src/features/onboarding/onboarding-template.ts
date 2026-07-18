import { dom } from "../../app/dom-builder";

export function mountOnboardingTemplate(document: Document): HTMLElement {
  const title = dom(document, "span", { className: "ob-title" });
  const skip = dom(document, "span", { className: "x ob-skip", text: "Skip" });
  const head = dom(document, "div", { className: "ob-head" }, [title, skip]);
  const progressBar = dom(document, "div", { className: "ob-progress-bar" });
  const progress = dom(document, "div", { className: "ob-progress" }, [progressBar]);
  const body = dom(document, "div", { className: "ob-body" });
  const previous = dom(document, "button", { className: "ob-prev", text: "Back" });
  const next = dom(document, "button", { className: "ob-next", text: "Next" });
  const actions = dom(document, "div", { className: "ob-actions" }, [previous, next]);
  const box = dom(document, "div", { className: "ob-box" }, [head, progress, body, actions]);
  return dom(document, "div", { id: "onboarding" }, [box]);
}
