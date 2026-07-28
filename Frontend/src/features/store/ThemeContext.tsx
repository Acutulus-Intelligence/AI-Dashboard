import type { ReactNode } from 'react';
import { ThemeProvider as NextThemesProvider } from 'next-themes';
import { THEME_STORAGE_KEY } from './useTheme';

/**
 * Applies `.dark` on <html> based on the stored preference, falling back to the
 * browser's `prefers-color-scheme`. The matching pre-paint script lives in
 * index.html and must stay in sync with THEME_STORAGE_KEY.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  return (
    <NextThemesProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      enableColorScheme
      storageKey={THEME_STORAGE_KEY}
      disableTransitionOnChange
    >
      {children}
    </NextThemesProvider>
  );
}
