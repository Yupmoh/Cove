export interface DomOptions {
  id?: string;
  className?: string;
  text?: string;
  title?: string;
  attributes?: Readonly<Record<string, string>>;
}

export function dom<K extends keyof HTMLElementTagNameMap>(
  document: Document,
  tagName: K,
  options: DomOptions = {},
  children: ReadonlyArray<Node> = [],
): HTMLElementTagNameMap[K] {
  const element = document.createElement(tagName);
  if (options.id) element.id = options.id;
  if (options.className) element.className = options.className;
  if (options.text !== undefined) element.textContent = options.text;
  if (options.title) element.title = options.title;
  for (const [name, value] of Object.entries(options.attributes ?? {})) element.setAttribute(name, value);
  element.append(...children);
  return element;
}
