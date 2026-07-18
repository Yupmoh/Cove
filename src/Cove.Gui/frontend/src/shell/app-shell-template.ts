import { dom } from "../app/dom-builder";

export function mountTitlebarTemplate(document: Document): HTMLElement {
  const trafficLightZone = dom(document, "div", { id: "tl-zone" });
  const wordmarkImage = dom(document, "img", {
    id: "wordmark-img",
    attributes: { alt: "cove", "data-webview-ignore": "" },
  });
  const wordmarkVersion = dom(document, "span", { id: "wordmark-ver" });
  const wordmark = dom(document, "div", { id: "wordmark" }, [wordmarkImage, wordmarkVersion]);
  const cluster = dom(document, "div", { id: "tb-cluster" });
  const right = dom(document, "div", { id: "tb-right" });
  const minimize = dom(document, "div", {
    id: "wc-min",
    className: "wc",
    text: "–",
    title: "Minimize",
    attributes: { "data-webview-minimize": "" },
  });
  const maximize = dom(document, "div", {
    id: "wc-max",
    className: "wc",
    text: "□",
    title: "Maximize",
    attributes: { "data-webview-maximize": "" },
  });
  const close = dom(document, "div", {
    id: "wc-close",
    className: "wc wc-close",
    text: "✕",
    title: "Close",
    attributes: { "data-webview-close": "" },
  });
  const controls = dom(document, "div", { id: "win-controls" }, [minimize, maximize, close]);
  return dom(document, "div", {
    id: "titlebar",
    attributes: { "data-webview-drag": "" },
  }, [trafficLightZone, wordmark, cluster, right, controls]);
}

export function mountWorkspaceTemplate(document: Document): HTMLElement {
  const rail = dom(document, "div", { id: "left-rail", className: "sb-rail" });
  const content = dom(document, "div", { id: "left-content", className: "sb-content" });
  const resize = dom(document, "div", { id: "left-resize", className: "sb-resize", title: "Resize" });
  const sidebar = dom(document, "aside", {
    id: "left-sidebar",
    className: "dual-sidebar side-left",
    attributes: { "data-webview-ignore": "" },
  }, [rail, content, resize]);
  const shoreTabs = dom(document, "div", { id: "shore-tabs" });
  const shoresRow = dom(document, "div", { id: "shores-row" }, [shoreTabs]);
  const grid = dom(document, "div", { id: "grid" });
  const main = dom(document, "div", { id: "main" }, [shoresRow, grid]);
  return dom(document, "div", { id: "app-row" }, [sidebar, main]);
}

export function mountPaletteTemplate(document: Document): HTMLElement {
  const input = dom(document, "input", {
    id: "pal-input",
    attributes: {
      placeholder: "Type a command...",
      autocomplete: "off",
      spellcheck: "false",
    },
  });
  const list = dom(document, "div", { id: "pal-list" });
  const box = dom(document, "div", { className: "pal-box" }, [input, list]);
  return dom(document, "div", { id: "palette" }, [box]);
}

export function mountSettingsTemplate(document: Document): HTMLElement {
  const title = dom(document, "span", { text: "Settings" });
  const close = dom(document, "span", { id: "set-close", className: "x", text: "×" });
  const head = dom(document, "div", { className: "set-head" }, [title, close]);
  const navigation = dom(document, "nav", { id: "set-tabs", className: "set-nav" });
  const body = dom(document, "div", { id: "set-body", className: "set-body" });
  const main = dom(document, "div", { className: "set-main" }, [navigation, body]);
  const box = dom(document, "div", { className: "set-box set-box--nav" }, [head, main]);
  return dom(document, "div", { id: "settings" }, [box]);
}

export function mountWorkspaceCreationTemplate(document: Document): HTMLElement {
  const title = dom(document, "span", { text: "New Bay" });
  const kicker = dom(document, "span", {
    className: "wsc-kicker",
    text: "Create a home for a project and its Shores",
  });
  const heading = dom(document, "span", { className: "wsc-head-copy" }, [title, kicker]);
  const close = dom(document, "span", { id: "wsc-close", className: "x", text: "×" });
  const head = dom(document, "div", { className: "set-head" }, [heading, close]);
  const nameLabel = dom(document, "label", {
    text: "Name",
    attributes: { for: "wsc-name" },
  });
  const name = dom(document, "input", {
    id: "wsc-name",
    attributes: {
      type: "text",
      placeholder: "My project",
      autocomplete: "off",
      spellcheck: "false",
    },
  });
  const nameField = dom(document, "div", { className: "wsc-field" }, [nameLabel, name]);
  const pathLabel = dom(document, "label", {
    text: "Directory",
    attributes: { for: "wsc-path" },
  });
  const path = dom(document, "input", {
    id: "wsc-path",
    attributes: {
      type: "text",
      placeholder: "~/code/my-project",
      autocomplete: "off",
      spellcheck: "false",
    },
  });
  const browse = dom(document, "button", {
    id: "wsc-browse",
    text: "Browse…",
    attributes: { type: "button" },
  });
  const pathRow = dom(document, "div", { className: "wsc-path-row" }, [path, browse]);
  const hint = dom(document, "div", {
    className: "wsc-hint",
    text: "Use ~ for your home directory. Created if it does not exist.",
  });
  const pathField = dom(document, "div", { className: "wsc-field" }, [pathLabel, pathRow, hint]);
  const iconLabel = dom(document, "label", { text: "Icon" });
  const iconGrid = dom(document, "div", { id: "wsc-icon-grid", className: "ws-icon-grid" });
  const iconField = dom(document, "div", { className: "wsc-field" }, [iconLabel, iconGrid]);
  const error = dom(document, "div", { id: "wsc-error", className: "wsc-error" });
  const cancel = dom(document, "button", {
    id: "wsc-cancel",
    className: "secondary",
    text: "Cancel",
    attributes: { type: "button" },
  });
  const create = dom(document, "button", {
    id: "wsc-create",
    className: "primary",
    text: "Create Bay",
    attributes: { type: "button" },
  });
  const actions = dom(document, "div", { className: "wsc-actions" }, [cancel, create]);
  const body = dom(document, "div", { className: "set-body" }, [
    nameField,
    pathField,
    iconField,
    error,
    actions,
  ]);
  const box = dom(document, "div", { className: "set-box wsc-box" }, [head, body]);
  return dom(document, "div", { id: "ws-create" }, [box]);
}

export function mountPerformanceHudTemplate(document: Document): HTMLElement {
  return dom(document, "div", {
    id: "perf-hud",
    attributes: { "data-webview-ignore": "" },
  });
}
