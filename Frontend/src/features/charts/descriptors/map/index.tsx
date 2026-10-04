import { Map as MapIcon } from 'lucide-react';
import { type ChartDescriptor, type ParamSpec } from '../../types';
import ChartPlaceholder from '../ChartPlaceholder';
import { hasSeries, showTooltip } from '../params';
import MapView from './MapView';
import { buildMapRows } from './places';

const showLegend: ParamSpec = {
  kind: 'boolean',
  key: 'showLegend',
  label: 'Legend',
  default: true,
};

const showLabels: ParamSpec = {
  kind: 'boolean',
  key: 'showLabels',
  label: 'Place labels',
  default: false,
};

const fillOpacity: ParamSpec = {
  kind: 'number',
  key: 'fillOpacity',
  label: 'Fill opacity',
  default: 0.85,
  min: 0.2,
  max: 1,
  step: 0.05,
};

export const mapChart: ChartDescriptor = {
  id: 'map',
  label: 'Map',
  description: 'Shows a measure across places on a map.',
  icon: MapIcon,
  defaultSize: { w: 6, h: 5 },
  minSize: { w: 4, h: 4 },
  variants: [
    {
      id: 'choropleth',
      label: 'Choropleth',
      description: 'Countries colored by the measure.',
    },
    {
      id: 'markers',
      label: 'Markers',
      description: 'Points from coordinates, or country centres when a name matches.',
    },
    {
      id: 'grid',
      label: 'Grid',
      description: 'Squares on the map, colored by the measure.',
    },
  ],
  params: [showTooltip, showLegend, showLabels, fillOpacity],
  render({ data, style }) {
    if (!hasSeries(data)) return <ChartPlaceholder icon={MapIcon} label="Map" />;

    const { rows, truncated } = buildMapRows(data);
    const variant = style.variant === 'markers' || style.variant === 'grid' ? style.variant : 'choropleth';
    const datasetLabels = data.datasets.map((dataset) => dataset.label);

    return (
      <MapView
        variant={variant}
        rows={rows}
        style={style}
        measure={datasetLabels[0] ?? 'Value'}
        datasetLabels={datasetLabels}
        truncated={truncated}
      />
    );
  },
};
