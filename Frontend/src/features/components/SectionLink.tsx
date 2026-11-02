import type { ComponentPropsWithoutRef, MouseEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import {
  scrollToLandingSection,
  scrollToPageTop,
  type LandingSectionId,
  type ScrollToLocationState,
} from '../navigation/publicNav';
import { ROUTES } from '../routes';

type SectionLinkProps = Omit<ComponentPropsWithoutRef<typeof Link>, 'to'> & {
  to: string;
  sectionId?: LandingSectionId;
};

function isModifiedClick(event: MouseEvent<HTMLAnchorElement>) {
  return event.metaKey || event.altKey || event.ctrlKey || event.shiftKey || event.button !== 0;
}

/**
 * Route link that can also scroll to a landing section.
 * Section targets render `href="/"` and never write a `#fragment` into the URL.
 * On `/` they scroll in place. From any other page they navigate home and scroll.
 */
export default function SectionLink({ to, sectionId, onClick, ...rest }: SectionLinkProps) {
  const location = useLocation();
  const navigate = useNavigate();
  const href = sectionId ? ROUTES.HOME : to;

  const handleClick = (event: MouseEvent<HTMLAnchorElement>) => {
    onClick?.(event);
    if (event.defaultPrevented || isModifiedClick(event)) return;

    if (sectionId) {
      event.preventDefault();
      if (location.pathname === ROUTES.HOME) {
        const id = sectionId;
        requestAnimationFrame(() => scrollToLandingSection(id));
        return;
      }
      const state: ScrollToLocationState = { scrollTo: sectionId };
      navigate(ROUTES.HOME, { state });
      return;
    }

    if (to === ROUTES.HOME && location.pathname === ROUTES.HOME && !location.hash) {
      event.preventDefault();
      requestAnimationFrame(() => scrollToPageTop());
    }
  };

  return <Link to={href} onClick={handleClick} {...rest} />;
}
