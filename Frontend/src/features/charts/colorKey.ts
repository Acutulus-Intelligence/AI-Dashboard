/** Safe CSS custom-property suffix (no spaces / special chars). */
export function toCssColorIdent(key: string): string {
  const slug = key
    .trim()
    .replace(/[^a-zA-Z0-9_-]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 48);
  return slug || 'color';
}

/** Indexed key for category/slice colours (pie, radial). */
export function chartColorKey(index: number, label?: string): string {
  return `s${index}-${toCssColorIdent(label ?? 'slice')}`;
}

/** `var(--color-…)` reference matching ChartStyle output. */
export function cssColorVar(key: string): string {
  return `var(--color-${toCssColorIdent(key)})`;
}
