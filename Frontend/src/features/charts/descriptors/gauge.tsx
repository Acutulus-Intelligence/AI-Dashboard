import { Gauge as GaugeIcon } from 'lucide-react';
import { formatStyledValue } from '../format';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';

const minValue: ParamSpec = {
  kind: 'number',
  key: 'minValue',
  label: 'Minimum',
  default: 0,
  min: -1_000_000,
  max: 1_000_000,
  step: 1,
};

const maxValue: ParamSpec = {
  kind: 'number',
  key: 'maxValue',
  label: 'Maximum',
  default: 100,
  min: -1_000_000,
  max: 1_000_000,
  step: 1,
};

const thickness: ParamSpec = {
  kind: 'number',
  key: 'thickness',
  label: 'Arc thickness',
  default: 12,
  min: 4,
  max: 40,
  step: 2,
};

const autoRange: ParamSpec = {
  kind: 'boolean',
  key: 'autoRange',
  label: 'Auto range',
  default: false,
  help: 'Fit the range to the data instead of the min/max below.',
};

const showTarget: ParamSpec = {
  kind: 'boolean',
  key: 'showTarget',
  label: 'Target marker',
  default: false,
};

const targetValue: ParamSpec = {
  kind: 'number',
  key: 'targetValue',
  label: 'Target',
  default: 0,
  min: -1_000_000,
  max: 1_000_000,
  step: 1,
};

const CX = 100;
const CY = 96;
const RADIUS = 72;
const START_ANGLE = 180;
const END_ANGLE = 360;

function polarAt(angle: number, radius: number) {
  const radians = (angle * Math.PI) / 180;
  return {
    x: CX + radius * Math.cos(radians),
    y: CY + radius * Math.sin(radians),
  };
}

function polar(angle: number) {
  return polarAt(angle, RADIUS);
}

function arcPath(startAngle: number, endAngle: number): string {
  const start = polar(startAngle);
  const end = polar(endAngle);
  const largeArc = Math.abs(endAngle - startAngle) > 180 ? 1 : 0;
  return `M ${start.x} ${start.y} A ${RADIUS} ${RADIUS} 0 ${largeArc} 1 ${end.x} ${end.y}`;
}

function lastValue(values: (number | null)[]): number | null {
  for (let index = values.length - 1; index >= 0; index--) {
    if (values[index] != null) return values[index];
  }
  return null;
}

export const gaugeChart: ChartDescriptor = {
  id: 'gauge',
  label: 'Gauge',
  description: 'A single value against a fixed range.',
  icon: GaugeIcon,
  defaultSize: { w: 3, h: 3 },
  minSize: { w: 2, h: 2 },
  variants: [
    { id: 'default', label: 'Gauge', description: 'Semicircle showing the value against the range.' },
  ],
  params: [minValue, maxValue, thickness, autoRange, showTarget, targetValue],
  render({ data, style }) {
    const series = data.datasets[0];
    if (!series) return <ChartPlaceholder icon={GaugeIcon} label="Gauge" />;

    const value = lastValue(series.values);
    if (value == null) return <ChartPlaceholder icon={GaugeIcon} label="Gauge" />;

    const configuredMin = param(style, 'minValue', 0);
    const configuredMax = param(style, 'maxValue', 100);
    let low = Math.min(configuredMin, configuredMax);
    let high = Math.max(configuredMin, configuredMax);

    if (param(style, 'autoRange', false)) {
      let dataLow = Number.POSITIVE_INFINITY;
      let dataHigh = Number.NEGATIVE_INFINITY;
      for (const item of series.values) {
        if (item == null || !Number.isFinite(item)) continue;
        dataLow = Math.min(dataLow, item);
        dataHigh = Math.max(dataHigh, item);
      }
      if (Number.isFinite(dataLow)) {
        low = Math.min(0, dataLow);
        high = Math.max(dataHigh, low + 1);
      }
    }

    const range = high - low;
    const ratio = range === 0 ? 0 : Math.min(1, Math.max(0, (value - low) / range));
    const progressAngle = START_ANGLE + ratio * (END_ANGLE - START_ANGLE);
    const strokeWidth = param(style, 'thickness', 12);
    const target = param(style, 'targetValue', 0);
    const showTargetMarker =
      param(style, 'showTarget', false) && range > 0 && target >= low && target <= high;
    const targetAngle =
      START_ANGLE + ((target - low) / (range || 1)) * (END_ANGLE - START_ANGLE);
    const targetStart = polarAt(targetAngle, RADIUS - strokeWidth / 2 - 5);
    const targetEnd = polarAt(targetAngle, RADIUS + strokeWidth / 2 + 5);

    return (
      <div className="flex h-full w-full items-center justify-center">
        <svg
          viewBox="0 0 200 128"
          className="h-full w-full"
          role="img"
          aria-label={`${series.label}: ${formatStyledValue(value, style)}`}
        >
          <path
            d={arcPath(START_ANGLE, END_ANGLE)}
            fill="none"
            stroke="var(--muted)"
            strokeWidth={strokeWidth}
            strokeLinecap="round"
          />
          {ratio > 0 && (
            <path
              d={arcPath(START_ANGLE, progressAngle)}
              fill="none"
              stroke={style.colors[0]}
              strokeWidth={strokeWidth}
              strokeLinecap="round"
            />
          )}

          {showTargetMarker && (
            <line
              x1={targetStart.x}
              y1={targetStart.y}
              x2={targetEnd.x}
              y2={targetEnd.y}
              stroke="var(--foreground)"
              strokeWidth={2}
              strokeLinecap="round"
            />
          )}

          <text
            x={CX}
            y={CY - 10}
            textAnchor="middle"
            fontSize={26}
            fontWeight={600}
            className="fill-foreground"
          >
            {formatStyledValue(value, style)}
          </text>
          <text
            x={CX}
            y={CY + 10}
            textAnchor="middle"
            fontSize={10}
            className="fill-muted-foreground"
          >
            {series.label}
          </text>

          <text
            x={polar(START_ANGLE).x}
            y={CY + 16}
            textAnchor="start"
            fontSize={10}
            className="fill-muted-foreground"
          >
            {formatStyledValue(low, style)}
          </text>
          <text
            x={polar(END_ANGLE).x}
            y={CY + 16}
            textAnchor="end"
            fontSize={10}
            className="fill-muted-foreground"
          >
            {formatStyledValue(high, style)}
          </text>
        </svg>
      </div>
    );
  },
};
