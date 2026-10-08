declare module 'topojson-client' {
  import type { FeatureCollection, GeoJsonProperties, Geometry } from 'geojson';
  import type { GeometryObject, Topology } from 'topojson-specification';

  export function feature(
    topology: Topology,
    object: GeometryObject | GeometryObject<GeoJsonProperties>,
  ): FeatureCollection<Geometry, GeoJsonProperties>;
}

declare module 'world-atlas/countries-50m.json' {
  import type { Topology } from 'topojson-specification';
  const topology: Topology;
  export default topology;
}
