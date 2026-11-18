/** Delay before releasing the blob URL; Safari/Firefox start downloads async. */
const REVOKE_DELAY_MS = 1000;

/** Slugifies a chart title into a safe file-name stem (no extension). */
export function slugifyTitle(title: string): string {
  return title
    .trim()
    .replace(/[^\p{L}\p{N}._-]+/gu, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 64);
}

/** Hands a blob to the browser as a download and releases the URL afterwards. */
export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), REVOKE_DELAY_MS);
}
