import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { SidebarInset, SidebarProvider } from '@/components/ui/sidebar';
import AppHeader, { type Crumb } from './AppHeader';
import AppSidebar from './AppSidebar';

interface AppShellProps {
  breadcrumbs: Crumb[];
  children: ReactNode;
  onNewChart?: () => void;
  onNewDashboard?: () => void;
  /** Extra header controls, e.g. a page-level save button. */
  headerActions?: ReactNode;
  /** Replaces the default header entirely, used by dashboard edit mode. */
  header?: ReactNode;
  /** Drop the default padding when the page manages its own layout. */
  bare?: boolean;
  className?: string;
}

export default function AppShell({
  breadcrumbs,
  children,
  onNewChart,
  onNewDashboard,
  headerActions,
  header,
  bare = false,
  className,
}: AppShellProps) {
  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset className="min-w-0">
        {header ?? (
          <AppHeader
            breadcrumbs={breadcrumbs}
            onNewChart={onNewChart}
            onNewDashboard={onNewDashboard}
            actions={headerActions}
          />
        )}
        <main className={cn('flex min-w-0 flex-1 flex-col', !bare && 'gap-6 p-4 md:p-6', className)}>
          {children}
        </main>
      </SidebarInset>
    </SidebarProvider>
  );
}
