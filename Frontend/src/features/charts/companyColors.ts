/** Default company / individual chart colour swatches (matches backend DefaultColors). */
export const DEFAULT_COMPANY_COLORS: string[] = Array.from(
  { length: 8 },
  (_, i) => `var(--chart-${i + 1})`,
);

export const MAX_COMPANY_COLORS = 24;

export function colorLabel(color: string): string {
  if (color.startsWith('var(--chart-')) {
    const n = color.match(/chart-(\d+)/)?.[1];
    return n ? `Default ${n}` : color;
  }
  return color.toUpperCase();
}
