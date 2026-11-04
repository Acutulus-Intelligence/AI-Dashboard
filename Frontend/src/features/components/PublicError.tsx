import type { ReactNode } from 'react';
import { AlertCircle } from 'lucide-react';
import { publicErrorBannerClass } from '../layouts/publicChrome';

export default function PublicError({ children }: { children: ReactNode }) {
  return (
    <div className={publicErrorBannerClass} role="alert">
      <AlertCircle className="size-4 shrink-0" aria-hidden="true" />
      <span>{children}</span>
    </div>
  );
}
