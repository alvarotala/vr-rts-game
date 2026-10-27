type Child = Node | string | number | null | undefined | false;
type Props = Record<string, unknown> & { class?: string; style?: Partial<CSSStyleDeclaration>; dataset?: Record<string, string> };

// Minimal element builder: h('button', { class: 'primary', onclick }, 'Start').
export function h<K extends keyof HTMLElementTagNameMap>(tag: K, props: Props = {}, ...children: Child[]): HTMLElementTagNameMap[K] {
  const element = document.createElement(tag);
  for (const [key, value] of Object.entries(props)) {
    if (value === undefined || value === null || value === false) continue;
    if (key === 'class') element.className = String(value);
    else if (key === 'style') Object.assign(element.style, value);
    else if (key === 'dataset') Object.assign(element.dataset, value);
    else if (key.startsWith('on') && typeof value === 'function') element.addEventListener(key.slice(2), value as EventListener);
    else if (key in element) (element as unknown as Record<string, unknown>)[key] = value;
    else element.setAttribute(key, String(value));
  }
  for (const child of children) if (child !== null && child !== undefined && child !== false) element.append(child instanceof Node ? child : String(child));
  return element;
}

export function clear(element: HTMLElement): HTMLElement { element.replaceChildren(); return element; }

// Append children, skipping empty slots (null/undefined/false).
export function fill(element: HTMLElement, ...children: Child[]): HTMLElement {
  for (const child of children) if (child !== null && child !== undefined && child !== false) element.append(child instanceof Node ? child : String(child));
  return element;
}

export function formatTime(ticks: number): string {
  const seconds = Math.floor(ticks / 20);
  return Math.floor(seconds / 60) + ':' + String(seconds % 60).padStart(2, '0');
}
