import type { DecimalMode, ResolvedStyle } from './types';

type FormatStyle = Pick<
  ResolvedStyle,
  'valuePrefix' | 'valueSuffix' | 'decimals' | 'decimalMode'
>;

function applyDecimals(value: number, decimals: number, mode: DecimalMode): string {
  if (mode === 'truncate') {
    const factor = 10 ** decimals;
    const truncated = Math.trunc(value * factor) / factor;
    return truncated.toLocaleString(undefined, {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals,
    });
  }

  return value.toLocaleString(undefined, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  });
}

/** Formats a numeric chart value with optional prefix/suffix/decimals from style. */
export function formatStyledValue(value: unknown, style: FormatStyle): string {
  const raw =
    typeof value === 'number'
      ? value
      : typeof value === 'string' && value.trim() !== '' && !Number.isNaN(Number(value))
        ? Number(value)
        : value;

  const text =
    typeof raw === 'number' && Number.isFinite(raw)
      ? style.decimals != null
        ? applyDecimals(raw, style.decimals, style.decimalMode ?? 'round')
        : raw.toLocaleString()
      : String(value ?? '');

  return `${style.valuePrefix ?? ''}${text}${style.valueSuffix ?? ''}`;
}

export function tickFormatter(style: FormatStyle) {
  return (value: number | string) => formatStyledValue(value, style);
}
