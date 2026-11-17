import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { publicCardClass, publicPageClass } from './publicChrome';

export default function PublicAuthFrame({
  children,
  wide = false,
}: {
  children: ReactNode;
  wide?: boolean;
}) {
  return (
    <div className={cn('flex flex-1 items-center justify-center px-5 py-12 sm:px-6', publicPageClass)}>
      <div className={cn('w-full p-6 sm:p-8', wide ? 'max-w-lg' : 'max-w-md', publicCardClass)}>
        {children}
      </div>
    </div>
  );
}
