import { dom } from "../../app/dom-builder";

export function mountFindBarTemplate(document: Document): HTMLElement {
  const input = dom(document, "input", {
    id: "find-input",
    attributes: {
      placeholder: "Find",
      autocomplete: "off",
      spellcheck: "false",
    },
  });
  const previous = dom(document, "button", { id: "find-prev", text: "↑", title: "Previous" });
  const next = dom(document, "button", { id: "find-next", text: "↓", title: "Next" });
  const close = dom(document, "button", { id: "find-close", text: "×", title: "Close" });
  return dom(document, "div", { id: "findbar" }, [input, previous, next, close]);
}
