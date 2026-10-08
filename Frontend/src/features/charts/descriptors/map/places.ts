import type { ChartData } from '../../types';
import { COUNTRIES } from './countries';

/** Rows drawn on a map. Coordinates are optional; countryId is set when the label matches. */
export interface MapRow {
  label: string;
  values: number[];
  lat: number | null;
  lon: number | null;
  countryId: string | null;
}

const MAX_ROWS = 500;

/** Folds accents and punctuation so "Côte d'Ivoire" and "U.S.A." match the catalog. */
export function normalizePlace(value: string): string {
  const folded = value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/&/g, ' and ')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, ' ')
    .trim()
    .replace(/\s+/g, ' ')
    .replace(/^the /, '');

  const tokens = folded.split(' ');
  if (tokens.length > 1 && tokens.every((token) => token.length === 1)) return tokens.join('');
  return folded;
}

const byKey = new Map<string, string>();

function remember(key: string, id: string) {
  if (!key || byKey.has(key)) return;
  byKey.set(key, id);
}

for (const country of COUNTRIES) {
  for (const name of country.names) remember(normalizePlace(name), country.id);
  remember(country.iso2.toLowerCase(), country.id);
  remember(country.iso3.toLowerCase(), country.id);
  remember(country.id, country.id);
  if (/^\d+$/.test(country.id)) remember(String(Number(country.id)), country.id);
}

export function matchCountryId(label: string): string | null {
  const key = normalizePlace(label);
  if (!key) return null;
  return byKey.get(key) ?? null;
}

function normalizeColumn(key: string): string {
  return key.trim().toLowerCase().replace(/[\s-]+/g, '_');
}

function isLatitudeColumn(key: string): boolean {
  const normalized = normalizeColumn(key);
  return (
    normalized === 'lat' ||
    normalized === 'latitude' ||
    normalized.endsWith('_lat') ||
    normalized.endsWith('_latitude') ||
    normalized.includes('latitude')
  );
}

function isLongitudeColumn(key: string): boolean {
  const normalized = normalizeColumn(key);
  return (
    normalized === 'lon' ||
    normalized === 'lng' ||
    normalized === 'longitude' ||
    normalized.endsWith('_lon') ||
    normalized.endsWith('_lng') ||
    normalized.endsWith('_longitude') ||
    normalized.includes('longitude')
  );
}

function findColumn(row: Record<string, unknown>, matches: (key: string) => boolean): string | undefined {
  return Object.keys(row).find(matches);
}

function readNumber(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
}

export function primaryValue(values: number[]): number {
  return values[0] ?? 0;
}

export function buildMapRows(data: ChartData): { rows: MapRow[]; truncated: boolean } {
  const sample = data.queryResult?.find((row) => row && Object.keys(row).length > 0);
  const latKey = sample ? findColumn(sample, isLatitudeColumn) : undefined;
  const lonKey = sample ? findColumn(sample, isLongitudeColumn) : undefined;
  const limit = Math.min(data.labels.length, MAX_ROWS);

  const rows: MapRow[] = [];
  for (let i = 0; i < limit; i++) {
    const label = data.labels[i] ?? '';
    const values = data.datasets.map((dataset) => dataset.values[i] ?? 0);
    const source = data.queryResult?.[i];
    let lat: number | null = null;
    let lon: number | null = null;
    if (source && latKey && lonKey) {
      const nextLat = readNumber(source[latKey]);
      const nextLon = readNumber(source[lonKey]);
      if (nextLat != null && nextLon != null && Math.abs(nextLat) <= 90 && Math.abs(nextLon) <= 180) {
        lat = nextLat;
        lon = nextLon;
      }
    }
    rows.push({ label, values, lat, lon, countryId: matchCountryId(label) });
  }

  return { rows, truncated: data.labels.length > MAX_ROWS };
}

export function aggregateCountries(rows: MapRow[]): Map<string, { label: string; values: number[] }> {
  const countries = new Map<string, { label: string; values: number[] }>();
  for (const row of rows) {
    if (!row.countryId) continue;
    const current = countries.get(row.countryId);
    if (!current) {
      countries.set(row.countryId, { label: row.label, values: [...row.values] });
      continue;
    }
    current.values = current.values.map((value, index) => value + (row.values[index] ?? 0));
  }
  return countries;
}

export function mapFootnote(labels: string[], truncated: boolean): string | null {
  const unique = [...new Set(labels.map((label) => label.trim()).filter(Boolean))];
  const parts: string[] = [];
  if (truncated) parts.push('Showing the first 500 rows.');
  if (unique.length > 0) {
    const preview = unique.slice(0, 6).join(', ');
    const extra = unique.length > 6 ? ` +${unique.length - 6} more` : '';
    parts.push(`${unique.length} not shown on the map: ${preview}${extra}`);
  }
  return parts.length > 0 ? parts.join(' ') : null;
}
