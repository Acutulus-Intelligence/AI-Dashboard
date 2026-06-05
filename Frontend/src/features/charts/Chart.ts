import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';

export abstract class Chart {
  abstract readonly id: string;
  abstract readonly label: string;
  abstract readonly icon: LucideIcon;
  abstract readonly defaultSize: { w: number; h: number };
  readonly minSize: { w: number; h: number } = { w: 2, h: 2 };

  abstract render(data?: unknown): ReactNode;
}
