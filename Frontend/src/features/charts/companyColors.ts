/** Default company / individual chart colour swatches (matches backend DefaultColors). */
export const DEFAULT_COMPANY_COLORS: string[] = Array.from(
  { length: 8 },
  (_, i) => `var(--chart-${i + 1})`,
);

export const MAX_COMPANY_COLORS = 24;

const HEX_RE = /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/;

function normalizeHex(hex: string): string {
  const h = hex.trim();
  if (h.length === 4 || h.length === 5) {
    return `#${h[1]}${h[1]}${h[2]}${h[2]}${h[3]}${h[3]}`.toLowerCase();
  }
  return (h.length >= 7 ? h.slice(0, 7) : h).toLowerCase();
}

/**
 * Resolves CSS tokens like `var(--chart-1)` to `#rrggbb` for UI labels.
 */
export function cssColorToHex(cssColor: string): string {
  const trimmed = cssColor.trim();
  if (!trimmed) return trimmed;
  if (HEX_RE.test(trimmed)) return normalizeHex(trimmed);
  if (typeof document === 'undefined') return trimmed;

  const tokenMatch = trimmed.match(/^var\(\s*(--chart-[1-8])\s*\)$/i);
  let toResolve = trimmed;
  if (tokenMatch) {
    const raw = getComputedStyle(document.documentElement).getPropertyValue(tokenMatch[1]).trim();
    if (raw) toResolve = raw;
  }

  const el = document.createElement('div');
  el.style.color = toResolve;
  el.style.position = 'absolute';
  el.style.left = '-9999px';
  el.style.visibility = 'hidden';
  el.style.pointerEvents = 'none';
  document.body.appendChild(el);
  const computed = getComputedStyle(el).color;
  document.body.removeChild(el);

  const match = computed.match(/rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)/i);
  if (!match) return trimmed;

  const r = Math.round(Number(match[1]));
  const g = Math.round(Number(match[2]));
  const b = Math.round(Number(match[3]));
  return `#${[r, g, b].map((n) => n.toString(16).padStart(2, '0')).join('')}`;
}

/** Display label — always hex when possible (theme defaults and custom swatches alike). */
export function colorLabel(color: string): string {
  const hex = cssColorToHex(color);
  if (HEX_RE.test(hex)) return hex.toUpperCase();
  if (color.startsWith('var(--chart-')) {
    const n = color.match(/chart-(\d+)/)?.[1];
    return n ? `Default ${n}` : color;
  }
  return color.toUpperCase();
}
