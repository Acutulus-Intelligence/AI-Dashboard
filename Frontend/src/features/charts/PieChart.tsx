import type { ReactNode } from 'react';
import { PieChart as PieIcon } from 'lucide-react';
import {
  PieChart as RechartsPieChart,
  Pie,
  Cell,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';
import { Chart } from './Chart';

const COLORS = ['#00687a', '#3980f4', '#06b6d4', '#f59e0b', '#10b981', '#ef4444', '#8b5cf6'];

export class PieChartType extends Chart {
  readonly id = 'pie';
  readonly label = 'Pie Chart';
  readonly icon = PieIcon;
  readonly defaultSize = { w: 5, h: 4 };

  render(data?: unknown): ReactNode {
    const chartData = data as { labels: string[]; datasets: { label: string; values: number[] }[] } | undefined;
    if (!chartData?.labels?.length) return this.placeholder();

    const formatted = chartData.labels.map((label, i) => ({
      name: label,
      value: chartData.datasets[0]?.values[i] ?? 0,
    }));

    return (
      <ResponsiveContainer width="100%" height="100%">
        <RechartsPieChart>
          <Pie data={formatted} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={70} label={({ name, percent }) => `${name} ${((percent ?? 0) * 100).toFixed(0)}%`}>
            {formatted.map((_, i) => (
              <Cell key={i} fill={COLORS[i % COLORS.length]} />
            ))}
          </Pie>
          <Tooltip />
        </RechartsPieChart>
      </ResponsiveContainer>
    );
  }

  private placeholder() {
    return (
      <div className="flex h-full items-center justify-center text-on-surface-variant">
        <div className="text-center">
          <PieIcon size={48} className="mx-auto mb-2 text-secondary/40" />
          <p className="text-body-sm font-medium">Pie Chart</p>
        </div>
      </div>
    );
  }
}
