import type { Geometry, Position } from 'geojson';

function outerRings(geometry: Geometry): Position[][] {
  if (geometry.type === 'Polygon') return geometry.coordinates[0] ? [geometry.coordinates[0]] : [];
  if (geometry.type === 'MultiPolygon') {
    return geometry.coordinates.flatMap((polygon) => (polygon[0] ? [polygon[0]] : []));
  }
  return [];
}

/** Prefer the largest outline so overseas fragments do not pull a country marker offshore. */
export function geometryCenter(geometry: Geometry): { lat: number; lon: number } | null {
  const rings = outerRings(geometry);
  if (rings.length === 0) return null;

  let best = rings[0];
  let bestScore = -1;
  for (const ring of rings) {
    let score = 0;
    for (let i = 1; i < ring.length; i++) {
      score += Math.hypot(ring[i][0] - ring[i - 1][0], ring[i][1] - ring[i - 1][1]);
    }
    if (score > bestScore) {
      best = ring;
      bestScore = score;
    }
  }

  let minLon = Infinity;
  let maxLon = -Infinity;
  let minLat = Infinity;
  let maxLat = -Infinity;
  for (const [lon, lat] of best) {
    if (lon < minLon) minLon = lon;
    if (lon > maxLon) maxLon = lon;
    if (lat < minLat) minLat = lat;
    if (lat > maxLat) maxLat = lat;
  }
  if (!Number.isFinite(minLon) || !Number.isFinite(minLat)) return null;
  return { lon: (minLon + maxLon) / 2, lat: (minLat + maxLat) / 2 };
}
