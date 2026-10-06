import { lazy, Suspense } from 'react';
import type { ResolvedStyle } from '../../types';
import type { MapRow } from './places';

const GeographicMap = lazy(() => import('./GeographicMap'));

interface MapViewProps {
  variant: 'choropleth' | 'markers' | 'grid';
  rows: MapRow[];
  style: ResolvedStyle;
  measure: string;
  datasetLabels: string[];
  truncated: boolean;
}

export default function MapView({
  variant,
  rows,
  style,
  measure,
  datasetLabels,
  truncated,
}: MapViewProps) {
  return (
    <Suspense
      fallback={
        <div className="text-muted-foreground flex h-full items-center justify-center text-sm">
          Loading map…
        </div>
      }
    >
      <GeographicMap
        variant={variant}
        rows={rows}
        style={style}
        measure={measure}
        datasetLabels={datasetLabels}
        truncated={truncated}
      />
    </Suspense>
  );
}
