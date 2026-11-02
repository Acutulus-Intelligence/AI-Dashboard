import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { publicEyebrowClass, publicLeadClass, publicTitleClass } from './publicChrome';

export default function PublicPageIntro({
  eyebrow,
  title,
  children,
  align = 'center',
}: {
  eyebrow: string;
  title: string;
  children?: ReactNode;
  align?: 'center' | 'start';
}) {
  return (
    <div className={align === 'center' ? 'mx-auto max-w-3xl text-center' : 'max-w-3xl text-left'}>
      <p className={publicEyebrowClass}>{eyebrow}</p>
      <h1 className={cn('mt-4', publicTitleClass)}>{title}</h1>
      {children ? <div className={cn('mt-4', publicLeadClass)}>{children}</div> : null}
    </div>
  );
}
