import type { ChartData, ChartDescriptor, ChartStyleConfig, ColorSlot } from './types';

export type ColorAxis = 'series' | 'category';

export interface ColorAxisInfo {
  axis: ColorAxis;
  /** Keys in the same order as `ResolvedStyle.colors`. Empty = positional only. */
  keys: string[];
  slots: ColorSlot[];
}

function slotKeys(
  entries: { index: number; label: string }[],
  fallback: (index: number) => string,
): ColorSlot[] {
  const counts = new Map<string, number>();
  for (const entry of entries) {
    const key = entry.label.trim();
    if (key) counts.set(key, (counts.get(key) ?? 0) + 1);
  }

  return entries.map(({ index, label }) => {
    const trimmed = label.trim();
    const unique = trimmed.length > 0 && counts.get(trimmed) === 1;
    return {
      index,
      key: unique ? trimmed : '',
      label: trimmed || fallback(index),
    };
  });
}

/**
 * Decides whether a chart colours series (one colour per dataset) or categories
 * (one colour per x-axis label / slice), then returns the ordered colour slots.
 */
export function resolveColorAxis(
  descriptor: ChartDescriptor,
  data: ChartData,
  config: ChartStyleConfig | undefined,
): ColorAxisInfo {
  const dynamic =
    descriptor.colorAxisParam &&
    config?.params?.[descriptor.colorAxisParam] === 'category' &&
    data.datasets.length <= 1 &&
    config?.variant !== 'stacked';

  const axis: ColorAxis =
    descriptor.colorAxis === 'category' || dynamic ? 'category' : 'series';

  if (axis === 'category') {
    const entries = data.labels
      .map((label, index) => ({ label: String(label ?? ''), index }))
      .filter(({ index }) => descriptor.colorSlotFilter?.(data, index) ?? true);
    const slots = slotKeys(entries, (index) => `Category ${index + 1}`);
    return { axis, keys: slots.map((slot) => slot.key), slots };
  }

  const entries = data.datasets.map((dataset, index) => ({
    label: String(dataset.label ?? ''),
    index,
  }));
  const slots = slotKeys(entries, (index) => `Series ${index + 1}`);
  return { axis, keys: slots.map((slot) => slot.key), slots };
}
