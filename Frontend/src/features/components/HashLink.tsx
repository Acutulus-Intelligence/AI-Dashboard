import { useNavigate } from 'react-router-dom';
import type { ReactNode, MouseEvent } from 'react';

interface HashLinkProps {
  to: string;
  children: ReactNode;
  className?: string;
}

export default function HashLink({ to, children, className }: HashLinkProps) {
  const navigate = useNavigate();

  const handleClick = (e: MouseEvent<HTMLAnchorElement>) => {
    e.preventDefault();

    const hashIndex = to.indexOf('#');
    const path = hashIndex > -1 ? to.slice(0, hashIndex) : to;
    const hash = hashIndex > -1 ? to.slice(hashIndex) : '';

    if (hash) {
      const el = document.getElementById(hash.slice(1));
      if (el) {
        el.scrollIntoView({ behavior: 'smooth' });
      }
    }

    if (path || hash) {
      navigate(to, { replace: true });
    }
  };

  return (
    <a href={to} onClick={handleClick} className={className}>
      {children}
    </a>
  );
}
