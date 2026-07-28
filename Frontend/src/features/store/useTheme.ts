import { useTheme as useNextTheme } from 'next-themes';

export const THEME_STORAGE_KEY = 'theme';

export type ThemeMode = 'system' | 'light' | 'dark';

export function useTheme() {
  const { theme, setTheme, resolvedTheme, systemTheme } = useNextTheme();
  return {
    mode: (theme ?? 'system') as ThemeMode,
    setMode: (next: ThemeMode) => setTheme(next),
    resolved: (resolvedTheme ?? systemTheme ?? 'light') as 'light' | 'dark',
  };
}
