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
    typeof raw === 'number' && Number.isFinite(raw) ? formatNumber(raw, style) : String(value ?? '');

  return `${style.valuePrefix ?? ''}${text}${style.valueSuffix ?? ''}`;
}

/**
 * Large values are abbreviated (42k, 1.2M) unless the user asked for fixed
 * decimals, so axes and KPI cards stay readable at a glance.
 */
function formatNumber(value: number, style: FormatStyle): string {
  if (style.decimals != null) {
    return applyDecimals(value, style.decimals, style.decimalMode ?? 'round');
  }

  if (Math.abs(value) >= 10_000) {
    return value.toLocaleString(undefined, {
      notation: 'compact',
      maximumFractionDigits: 1,
    });
  }

  return value.toLocaleString();
}

export function tickFormatter(style: FormatStyle) {
  return (value: number | string) => formatStyledValue(value, style);
}
