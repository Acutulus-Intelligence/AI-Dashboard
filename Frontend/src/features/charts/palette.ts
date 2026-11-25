import type {
  ChartDescriptor,
  ChartStyleConfig,
  NumberNotation,
  ResolvedStyle,
} from './types';

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

const THEME_COLORS = Array.from({ length: 16 }, (_, i) => token(i + 1));

export const THEME_CHART_COLORS = THEME_COLORS;

/** Golden-angle distinct fallback colours for charts with more than 16 categories. */
export function distinctChartColor(index: number): string {
  const hue = (index * 137.508) % 360;
  const saturation = 0.62;
  const lightness = index % 2 === 0 ? 0.48 : 0.6;

  const c = (1 - Math.abs(2 * lightness - 1)) * saturation;
  const x = c * (1 - Math.abs(((hue / 60) % 2) - 1));
  const m = lightness - c / 2;
  const [r, g, b] =
    hue < 60
      ? [c, x, 0]
      : hue < 120
        ? [x, c, 0]
        : hue < 180
          ? [0, c, x]
          : hue < 240
            ? [0, x, c]
            : hue < 300
              ? [x, 0, c]
              : [c, 0, x];

  const toHex = (value: number) =>
    Math.round((value + m) * 255)
      .toString(16)
      .padStart(2, '0');
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
}

export const PALETTES: Palette[] = [
  {
    id: 'default',
    label: 'Default',
    colors: THEME_COLORS,
  },
  {
    id: 'cool',
    label: 'Cool',
    colors: [1, 7, 11, 8, 13, 15, 3, 10].map(token),
  },
  {
    id: 'warm',
    label: 'Warm',
    colors: [2, 5, 6, 14, 12, 9, 4, 13].map(token),
  },
  {
    id: 'contrast',
    label: 'High contrast',
    colors: [1, 6, 3, 4, 5, 2, 7, 8, 9, 10, 11, 12].map(token),
  },
  {
    id: 'colorblind',
    label: 'Colorblind safe',
    // Okabe–Ito, the standard colour-blind safe qualitative palette.
    colors: [
      '#0072b2',
      '#e69f00',
      '#009e73',
      '#cc79a7',
      '#56b4e9',
      '#d55e00',
      '#f0e442',
      '#7a7a7a',
    ],
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

/** Effective colours for an ordered list of series/category keys. */
export function resolveColorSlots(
  config: ChartStyleConfig | undefined,
  colorKeys: string[],
): string[] {
  const palette = getPalette(config?.palette);
  const count = Math.max(colorKeys.length, 1);
  const keyed = config?.colorsByKey;
  return Array.from({ length: count }, (_, i) => {
    const key = colorKeys[i];
    const keyedColor = key && keyed ? (keyed[key] ?? keyed[key.trim()]) : undefined;
    const paletteColor =
      i < palette.colors.length ? palette.colors[i] : distinctChartColor(i);
    return keyedColor || config?.colors?.[i] || paletteColor;
  });
}

/**
 * Merges a saved style config over the descriptor defaults so renderers can read
 * every value without null checks.
 *
 * `colorKeys` is the ordered list of series or category names that receive
 * colours. Name-keyed overrides win over positional ones, which keeps a colour
 * attached to "Apple" even when rows are re-sorted.
 */
export function resolveStyle(
  descriptor: ChartDescriptor,
  config: ChartStyleConfig | undefined,
  colorKeys: string[],
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
  const colors = resolveColorSlots(config, colorKeys);

  const variant =
    descriptor.variants.find((v) => v.id === config?.variant)?.id ?? descriptor.variants[0].id;

  const decimals =
    typeof config?.decimals === 'number' && Number.isFinite(config.decimals)
      ? Math.min(10, Math.max(0, Math.trunc(config.decimals)))
      : null;

  const notation: NumberNotation =
    config?.notation === 'full' || config?.notation === 'compact'
      ? config.notation
      : 'auto';

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
    notation,
  };
}
