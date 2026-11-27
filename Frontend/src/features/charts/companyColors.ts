/** Default company / individual chart colour swatches (matches backend DefaultColors). */
export const DEFAULT_COMPANY_COLORS: string[] = Array.from(
  { length: 12 },
  (_, i) => `var(--chart-${i + 1})`,
);

export const MAX_COMPANY_COLORS = 24;

const HEX_RE = /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/;
const CHART_TOKEN_RE = /^var\(\s*--chart-(1[0-6]|[1-9])\s*\)$/i;

function normalizeHex(hex: string): string {
  const h = hex.trim();
  if (h.length === 4 || h.length === 5) {
    return `#${h[1]}${h[1]}${h[2]}${h[2]}${h[3]}${h[3]}`.toLowerCase();
  }
  return (h.length >= 7 ? h.slice(0, 7) : h).toLowerCase();
}

export function isHexColor(value: string): boolean {
  return HEX_RE.test(value.trim());
}

export function isChartToken(value: string): boolean {
  return CHART_TOKEN_RE.test(value.trim());
}

/**
 * Resolves CSS tokens like `var(--chart-1)` to `#rrggbb` for UI labels.
 */
export function cssColorToHex(cssColor: string): string {
  const trimmed = cssColor.trim();
  if (!trimmed) return trimmed;
  if (HEX_RE.test(trimmed)) return normalizeHex(trimmed);
  if (typeof document === 'undefined') return trimmed;

  const tokenMatch = trimmed.match(CHART_TOKEN_RE);
  let toResolve = trimmed;
  if (tokenMatch) {
    const raw = getComputedStyle(document.documentElement)
      .getPropertyValue(`--chart-${tokenMatch[1]}`)
      .trim();
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

function channelLuminance(channel: number): number {
  const value = channel / 255;
  return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
}

function relativeLuminance(hex: string): number | null {
  const match = hex.match(/^#([0-9a-f]{6})$/i);
  if (!match) return null;
  const value = match[1];
  const r = channelLuminance(parseInt(value.slice(0, 2), 16));
  const g = channelLuminance(parseInt(value.slice(2, 4), 16));
  const b = channelLuminance(parseInt(value.slice(4, 6), 16));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** WCAG contrast ratio between two CSS colours. Returns 21 when unknown. */
export function contrastRatio(foreground: string, background: string): number {
  const fg = relativeLuminance(cssColorToHex(foreground));
  const bg = relativeLuminance(cssColorToHex(background));
  if (fg == null || bg == null) return 21;

  const lighter = Math.max(fg, bg);
  const darker = Math.min(fg, bg);
  return (lighter + 0.05) / (darker + 0.05);
}
