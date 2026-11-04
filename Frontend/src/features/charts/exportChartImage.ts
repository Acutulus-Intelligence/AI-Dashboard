import { getFontEmbedCSS, toBlob } from 'html-to-image';

const DEFAULT_PIXEL_RATIO = 2;
const FALLBACK_BACKGROUND = '#ffffff';

let fontEmbedCssPromise: Promise<string> | null = null;

/** Embeds the web fonts once; the Google Fonts lookup is expensive to repeat. */
function loadFontEmbedCss(): Promise<string> {
  if (!fontEmbedCssPromise) {
    fontEmbedCssPromise = getFontEmbedCSS(document.body).catch(() => '');
  }
  return fontEmbedCssPromise;
}

/** Resolves the themed card colour so the transparent chart sits on a surface. */
function resolveCardBackground(): string {
  const value = getComputedStyle(document.documentElement)
    .getPropertyValue('--card')
    .trim();
  return value || FALLBACK_BACKGROUND;
}

/** Slugifies a chart title into a safe file-name stem (no extension). */
export function slugifyTitle(title: string): string {
  return title
    .trim()
    .replace(/[^a-zA-Z0-9._-]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 64);
}

export function chartTitleToFileName(title: string): string {
  return `${slugifyTitle(title) || 'chart'}.png`;
}

export interface ExportChartOptions {
  backgroundColor?: string;
  pixelRatio?: number;
}

/**
 * Rasterises a rendered chart node to a PNG the user downloads locally.
 * Colours applied through CSS custom properties are captured via computed
 * styles, so the snapshot matches what is shown in the frontend.
 */
export async function exportChartAsPng(
  node: HTMLElement | null | undefined,
  title: string,
  options: ExportChartOptions = {},
): Promise<void> {
  if (!node) {
    throw new Error('Chart is not ready to export.');
  }

  const fontEmbedCSS = await loadFontEmbedCss();

  const blob = await toBlob(node, {
    pixelRatio: options.pixelRatio ?? DEFAULT_PIXEL_RATIO,
    cacheBust: true,
    backgroundColor: options.backgroundColor ?? resolveCardBackground(),
    fontEmbedCSS,
  });

  if (!blob) {
    throw new Error('Could not render the chart image.');
  }

  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = chartTitleToFileName(title);
  anchor.click();
  URL.revokeObjectURL(url);
}
