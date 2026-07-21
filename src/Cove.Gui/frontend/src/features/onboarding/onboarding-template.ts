import { dom } from "../../app/dom-builder";

export function mountOnboardingTemplate(document: Document): HTMLElement {
  const title = dom(document, "h2", { id: "ob-step-title", className: "ob-title", attributes: { tabindex: "-1" } });
  const kicker = dom(document, "span", { className: "ob-kicker", text: "Getting started" });
  const heading = dom(document, "div", { className: "ob-heading" }, [kicker, title]);
  const skip = dom(document, "button", { className: "ob-skip", text: "Skip", attributes: { type: "button" } });
  const head = dom(document, "div", { className: "ob-head" }, [heading, skip]);
  const progressBar = dom(document, "progress", { className: "ob-progress-bar", attributes: { max: "100", value: "20", "aria-label": "Setup progress" } });
  const progress = dom(document, "div", { className: "ob-progress" }, [progressBar]);
  const body = dom(document, "div", { className: "ob-body ob-scroll-region" });
  const previous = dom(document, "button", { className: "ob-prev", text: "Back", attributes: { type: "button" } });
  const next = dom(document, "button", { className: "ob-next", text: "Next", attributes: { type: "button" } });
  const actions = dom(document, "div", { className: "ob-actions ob-fixed-footer" }, [previous, next]);
  const box = dom(document, "div", {
    className: "ob-box",
    attributes: {
      role: "dialog",
      "aria-modal": "true",
      "aria-labelledby": "ob-step-title",
      "aria-describedby": "ob-step-description",
    },
  }, [head, progress, body, actions]);
  return dom(document, "div", { id: "onboarding" }, [box]);
}
