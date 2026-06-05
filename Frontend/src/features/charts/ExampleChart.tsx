import type { ReactNode } from 'react';
import { BarChart3 } from 'lucide-react';
import { Chart } from './Chart';

export class ExampleChart extends Chart {
  readonly id = 'example';
  readonly label = 'Example Chart';
  readonly icon = BarChart3;
  readonly defaultSize = { w: 6, h: 4 };

  render(data?: unknown): ReactNode {
    void data;
    return (
      <div className="flex h-full items-center justify-center text-on-surface-variant">
        <div className="text-center">
          <BarChart3 size={48} className="mx-auto mb-2 text-secondary/40" />
          <p className="text-body-sm font-medium">Example Chart</p>
          <p className="text-xs text-on-surface-variant/60">Expand by creating a real implementation</p>
        </div>
      </div>
    );
  }
}
