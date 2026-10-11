import { useNavigate } from 'react-router-dom';
import type { ComponentPropsWithoutRef, MouseEvent } from 'react';

type HashLinkProps = Omit<ComponentPropsWithoutRef<'a'>, 'href'> & {
  to: string;
};

function scrollBehavior(): ScrollBehavior {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth';
}

export default function HashLink({ to, children, className, onClick, ...rest }: HashLinkProps) {
  const navigate = useNavigate();

  const handleClick = (event: MouseEvent<HTMLAnchorElement>) => {
    onClick?.(event);
    if (event.defaultPrevented) return;

    event.preventDefault();

    const hashIndex = to.indexOf('#');
    const path = hashIndex > -1 ? to.slice(0, hashIndex) : to;
    const hash = hashIndex > -1 ? to.slice(hashIndex) : '';

    if (hash) {
      const el = document.getElementById(hash.slice(1));
      if (el) {
        el.scrollIntoView({ behavior: scrollBehavior() });
      }
    }

    if (path || hash) {
      navigate(to, { replace: true });
    }
  };

  return (
    <a href={to} onClick={handleClick} className={className} {...rest}>
      {children}
    </a>
  );
}
