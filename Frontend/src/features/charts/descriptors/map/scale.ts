export function extent(values: number[]): { min: number; max: number } {
  let min = Infinity;
  let max = -Infinity;
  for (const value of values) {
    if (!Number.isFinite(value)) continue;
    if (value < min) min = value;
    if (value > max) max = value;
  }
  if (!Number.isFinite(min) || !Number.isFinite(max)) return { min: 0, max: 0 };
  return { min, max };
}

export function parseRgb(color: string): [number, number, number] {
  const match = color.match(/rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)/i);
  if (!match) return [100, 116, 139];
  return [Number(match[1]), Number(match[2]), Number(match[3])];
}

export function mixRgb(
  from: [number, number, number],
  to: [number, number, number],
  amount: number,
): string {
  const t = Math.min(1, Math.max(0, amount));
  const channel = (index: number) => Math.round(from[index] + (to[index] - from[index]) * t);
  return `rgb(${channel(0)}, ${channel(1)}, ${channel(2)})`;
}

export function colorAt(
  low: [number, number, number],
  high: [number, number, number],
  value: number,
  min: number,
  max: number,
): string {
  const raw = max === min ? 1 : (value - min) / (max - min);
  // Keep the lowest value visibly above the empty-land colour.
  const t = 0.28 + Math.min(1, Math.max(0, raw)) * 0.72;
  return mixRgb(low, high, t);
}

export function markerRadius(value: number, min: number, max: number): number {
  if (max === min) return 8;
  const t = Math.min(1, Math.max(0, (value - min) / (max - min)));
  return 5 + Math.sqrt(t) * 12;
}
