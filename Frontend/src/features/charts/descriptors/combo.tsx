import { BarChart3 } from 'lucide-react';
import { Bar, CartesianGrid, ComposedChart, LabelList, Line, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
} from '@/components/ui/chart';
import ChartHoverTooltip from '../ChartHoverTooltip';
import { cssColorVar } from '../colorKey';
import { tickFormatter } from '../format';
import { toChartConfig, toRows } from '../transform';
import { param, type ChartDescriptor, type ParamSpec } from '../types';
import ChartPlaceholder from './ChartPlaceholder';
import StyledChartTooltip from './StyledChartTooltip';
import {
  CARTESIAN_MARGIN,
  cornerRadius,
  hasSeries,
  showGrid,
  showLabels,
  showTooltip,
  showXAxis,
  showYAxis,
  strokeWidth,
} from './params';

const showLegend: ParamSpec = {
  kind: 'boolean',
  key: 'showLegend',
  label: 'Legend',
  default: true,
};

const dotSize: ParamSpec = {
  kind: 'number',
  key: 'dotSize',
  label: 'Point size',
  default: 3,
  min: 0,
  max: 8,
  step: 1,
  help: 'Zero hides the points on the line.',
};

export const comboChart: ChartDescriptor = {
  id: 'combo',
  label: 'Combo',
  description: 'Bars for the primary measures with one series drawn as a line.',
  icon: BarChart3,
  defaultSize: { w: 6, h: 4 },
  minSize: { w: 3, h: 3 },
  variants: [
    { id: 'default', label: 'Bar + line', description: 'The last series is drawn as a line.' },
    {
      id: 'lineFirst',
      label: 'Line first',
      description: 'The first series is drawn as a line; the rest are bars.',
    },
  ],
  params: [
    cornerRadius,
    strokeWidth,
    dotSize,
    showGrid,
    showXAxis,
    showYAxis,
    showTooltip,
    showLegend,
    showLabels,
  ],
  render({ data, style }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={BarChart3} label="Combo chart" />;

    const rows = toRows(data);
    const config = toChartConfig(data, style);
    const lineFirst = style.variant === 'lineFirst';
    const lastIndex = data.datasets.length - 1;
    const radius = param(style, 'cornerRadius', 4);
    const dot = param(style, 'dotSize', 3);

    return (
      <ChartContainer config={config} className="h-full w-full aspect-auto">
        <ComposedChart accessibilityLayer data={rows} margin={CARTESIAN_MARGIN}>
          {param(style, 'showGrid', true) && <CartesianGrid vertical={false} />}
          <XAxis
            dataKey="name"
            hide={!param(style, 'showXAxis', true)}
            tickLine={false}
            axisLine={false}
            tickMargin={8}
          />
          <YAxis
            hide={!param(style, 'showYAxis', true)}
            tickLine={false}
            axisLine={false}
            width={48}
            tickFormatter={tickFormatter(style)}
          />

          {param(style, 'showTooltip', true) && (
            <ChartHoverTooltip cursor={false} content={<StyledChartTooltip style={style} />} />
          )}
          {param(style, 'showLegend', true) && <ChartLegend content={<ChartLegendContent />} />}

          {data.datasets.map((ds, i) => {
            const isLine = lineFirst ? i === 0 : i === lastIndex;
            if (isLine) {
              return (
                <Line
                  key={ds.label}
                  dataKey={ds.label}
                  type="monotone"
                  stroke={cssColorVar(ds.label)}
                  strokeWidth={param(style, 'strokeWidth', 2)}
                  dot={dot > 0 ? { r: dot } : false}
                  activeDot={{ r: Math.max(dot, 4) }}
                >
                  {param(style, 'showLabels', false) && (
                    <LabelList
                      position="top"
                      offset={10}
                      className="fill-foreground"
                      fontSize={11}
                      formatter={(v) => tickFormatter(style)(v as number)}
                    />
                  )}
                </Line>
              );
            }

            return (
              <Bar
                key={ds.label}
                dataKey={ds.label}
                fill={cssColorVar(ds.label)}
                radius={radius}
              >
                {param(style, 'showLabels', false) && (
                  <LabelList
                    position="top"
                    className="fill-foreground"
                    fontSize={11}
                    formatter={(v) => tickFormatter(style)(v as number)}
                  />
                )}
              </Bar>
            );
          })}
        </ComposedChart>
      </ChartContainer>
    );
  },
};
