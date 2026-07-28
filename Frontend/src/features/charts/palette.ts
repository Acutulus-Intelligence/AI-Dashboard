import type { ChartDescriptor, ChartStyleConfig, ResolvedStyle } from './types';

/**
 * Palettes reference the `--chart-N` theme variables rather than literal colours
 * so every chart follows light/dark mode without re-saving its style config.
 */
export interface Palette {
  id: string;
  label: string;
  colors: string[];
}

const token = (n: number) => `var(--chart-${n})`;

export const PALETTES: Palette[] = [
  {
    id: 'default',
    label: 'Default',
    colors: [1, 2, 3, 4, 5, 6, 7, 8].map(token),
  },
  {
    id: 'cool',
    label: 'Cool',
    colors: [1, 7, 8, 4, 3].map(token),
  },
  {
    id: 'warm',
    label: 'Warm',
    colors: [2, 5, 6, 4, 3].map(token),
  },
  {
    id: 'contrast',
    label: 'High contrast',
    colors: [1, 6, 3, 4, 5, 2].map(token),
  },
  {
    id: 'mono',
    label: 'Monochrome',
    colors: [
      'var(--chart-1)',
      'color-mix(in oklch, var(--chart-1), var(--background) 25%)',
      'color-mix(in oklch, var(--chart-1), var(--background) 45%)',
      'color-mix(in oklch, var(--chart-1), var(--background) 62%)',
      'color-mix(in oklch, var(--chart-1), var(--background) 76%)',
    ],
  },
];

export const DEFAULT_PALETTE_ID = 'default';

export function getPalette(id: string | undefined): Palette {
  return PALETTES.find((p) => p.id === id) ?? PALETTES[0];
}

/**
 * Merges a saved style config over the descriptor defaults so renderers can read
 * every value without null checks.
 */
export function resolveStyle(
  descriptor: ChartDescriptor,
  config: ChartStyleConfig | undefined,
  seriesCount: number,
): ResolvedStyle {
  const params: Record<string, unknown> = {};
  for (const spec of descriptor.params) {
    params[spec.key] = spec.default;
  }
  if (config?.params) {
    for (const [key, value] of Object.entries(config.params)) {
      if (value !== undefined && value !== null) params[key] = value;
    }
  }

  const palette = getPalette(config?.palette);
  const colors = Array.from(
    { length: Math.max(seriesCount, 1) },
    (_, i) => config?.colors?.[i] || palette.colors[i % palette.colors.length],
  );

  const variant =
    descriptor.variants.find((v) => v.id === config?.variant)?.id ?? descriptor.variants[0].id;

  const decimals =
    typeof config?.decimals === 'number' && Number.isFinite(config.decimals)
      ? Math.min(10, Math.max(0, Math.trunc(config.decimals)))
      : null;

  return {
    variant,
    palette: palette.id,
    colors,
    params,
    valuePrefix: config?.valuePrefix ?? '',
    valueSuffix: config?.valueSuffix ?? '',
    info: config?.info ?? '',
    decimals,
    decimalMode: config?.decimalMode === 'truncate' ? 'truncate' : 'round',
  };
}
