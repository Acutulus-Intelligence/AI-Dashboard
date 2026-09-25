import type { Feature, FeatureCollection, Geometry } from 'geojson';
import type { Topology } from 'topojson-specification';
import { feature } from 'topojson-client';
import worldAtlas from 'world-atlas/countries-50m.json';
import { geometryCenter } from './geometry';

type CountryProps = { name?: string };

function canonicalFeatureId(item: Feature<Geometry, CountryProps>): string | null {
  const name = item.properties?.name ?? '';
  if (item.id != null && String(item.id) !== '') return String(item.id);
  if (name === 'Kosovo') return '383';
  if (name === 'N. Cyprus') return 'geo:N. Cyprus';
  if (name === 'Somaliland') return 'geo:Somaliland';
  return null;
}

function loadWorld(): FeatureCollection<Geometry, CountryProps> {
  const topology = worldAtlas as unknown as Topology;
  const object = topology.objects.countries;
  if (!object) return { type: 'FeatureCollection', features: [] };

  const collection = feature(topology, object) as FeatureCollection<Geometry, CountryProps>;
  return {
    type: 'FeatureCollection',
    features: collection.features.map((item) => {
      const id = canonicalFeatureId(item);
      return id ? { ...item, id } : item;
    }),
  };
}

export const WORLD = loadWorld();

export const CENTERS = new Map<string, { lat: number; lon: number }>();
export const WORLD_IDS = new Set<string>();

for (const item of WORLD.features) {
  if (item.id == null) continue;
  const id = String(item.id);
  WORLD_IDS.add(id);
  const center = geometryCenter(item.geometry);
  if (center) CENTERS.set(id, center);
}
