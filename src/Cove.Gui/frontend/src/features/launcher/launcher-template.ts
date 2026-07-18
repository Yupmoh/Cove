import { dom } from "../../app/dom-builder";

export function mountLauncherTemplate(document: Document): HTMLElement {
  const title = dom(document, "div", { className: "launch-title", text: "Open a nook" });
  const terminalIcon = dom(document, "span", { className: "ic", text: "▮" });
  const terminalLabel = dom(document, "span", { className: "lbl", text: "Terminal" });
  const terminal = dom(document, "div", { id: "launch-term", className: "launch-tile" }, [terminalIcon, terminalLabel]);
  const agents = dom(document, "div", { id: "launch-agents", className: "launch-tiles" });
  const tiles = dom(document, "div", { className: "launch-tiles" }, [terminal, agents]);
  const hint = dom(document, "div", {
    className: "launch-hint",
    text: "More nook types arrive in later milestones.",
  });
  const box = dom(document, "div", { className: "launch-box" }, [title, tiles, hint]);
  return dom(document, "div", { id: "launcher" }, [box]);
}
