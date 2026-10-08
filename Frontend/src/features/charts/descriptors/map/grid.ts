import { primaryValue } from './places';

interface LocatedPoint {
  label: string;
  values: number[];
  lat: number;
  lon: number;
}

/** One square on the map. Places that fall in the same cell are summed. */
export interface GridCell {
  south: number;
  west: number;
  north: number;
  east: number;
  values: number[];
  labels: string[];
}

/**
 * Bins points into a regular grid in screen space, then converts each occupied
 * cell back to geographic bounds. Empty cells are omitted so the basemap stays
 * visible; occupied cells stay aligned and scale with the current view.
 */
export function binScreenGrid(
  points: LocatedPoint[],
  width: number,
  height: number,
  toPoint: (lat: number, lon: number) => { x: number; y: number },
  toLatLng: (x: number, y: number) => { lat: number; lon: number },
  cellPx = 56,
): GridCell[] {
  if (points.length === 0 || width < 8 || height < 8) return [];

  const cols = Math.max(1, Math.round(width / cellPx));
  const rows = Math.max(1, Math.round(height / cellPx));
  const cellW = width / cols;
  const cellH = height / rows;
  const buckets: LocatedPoint[][] = Array.from({ length: rows * cols }, () => []);

  for (const point of points) {
    const { x, y } = toPoint(point.lat, point.lon);
    if (x < 0 || y < 0 || x >= width || y >= height) continue;
    const col = Math.min(cols - 1, Math.floor(x / cellW));
    const row = Math.min(rows - 1, Math.floor(y / cellH));
    buckets[row * cols + col].push(point);
  }

  const cells: GridCell[] = [];
  for (let row = 0; row < rows; row++) {
    for (let col = 0; col < cols; col++) {
      const inside = buckets[row * cols + col];
      if (inside.length === 0) continue;
      const northWest = toLatLng(col * cellW, row * cellH);
      const southEast = toLatLng((col + 1) * cellW, (row + 1) * cellH);
      const widthValues = inside.reduce((count, point) => Math.max(count, point.values.length), 0);
      cells.push({
        north: Math.max(northWest.lat, southEast.lat),
        south: Math.min(northWest.lat, southEast.lat),
        west: Math.min(northWest.lon, southEast.lon),
        east: Math.max(northWest.lon, southEast.lon),
        values: Array.from({ length: widthValues }, (_, index) =>
          inside.reduce((sum, point) => sum + (point.values[index] ?? 0), 0),
        ),
        labels: inside.map((point) => point.label),
      });
    }
  }

  return cells.sort((a, b) => primaryValue(b.values) - primaryValue(a.values));
}
