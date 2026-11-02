import { ROUTES } from '../routes';

/** Sections that live on `/` and are reached by scrolling, never by a URL fragment. */
export const LANDING_SECTION_IDS = ['product', 'demo', 'features', 'platform', 'testimonials'] as const;

export type LandingSectionId = (typeof LANDING_SECTION_IDS)[number];

export function isLandingSectionId(value: string): value is LandingSectionId {
  return (LANDING_SECTION_IDS as readonly string[]).includes(value);
}

export type PublicNavItem = {
  label: string;
  to: string;
  sectionId?: LandingSectionId;
};

/** Shared primary navigation for the marketing header and the app header. */
export const PUBLIC_NAV: readonly PublicNavItem[] = [
  { label: 'Home', to: ROUTES.HOME },
  { label: 'Features', to: ROUTES.HOME, sectionId: 'features' },
  { label: 'Demo', to: ROUTES.HOME, sectionId: 'demo' },
  { label: 'Pricing', to: ROUTES.PRICING },
  { label: 'Contact', to: ROUTES.CONTACT },
];

export function isPublicNavCurrent(pathname: string, item: PublicNavItem) {
  return !item.sectionId && item.to === pathname;
}

export type ScrollToLocationState = {
  scrollTo: LandingSectionId;
};

export function readScrollTarget(state: unknown): LandingSectionId | null {
  if (!state || typeof state !== 'object' || !('scrollTo' in state)) return null;
  const value = (state as { scrollTo?: unknown }).scrollTo;
  return typeof value === 'string' && isLandingSectionId(value) ? value : null;
}

export function scrollBehavior(): ScrollBehavior {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth';
}

export function scrollToLandingSection(id: LandingSectionId) {
  const el = document.getElementById(id);
  if (!el) return;
  el.scrollIntoView({ behavior: scrollBehavior(), block: 'start' });
}

export function scrollToPageTop() {
  window.scrollTo({ top: 0, behavior: scrollBehavior() });
}
