import { Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { SidebarTrigger } from '@/components/ui/sidebar';

interface DashboardEditHeaderProps {
  saving: boolean;
  onSave: () => void;
  onCancel: () => void;
  children?: React.ReactNode;
}

export default function DashboardEditHeader({
  saving,
  onSave,
  onCancel,
  children,
}: DashboardEditHeaderProps) {
  return (
    <header className="bg-accent/60 sticky top-0 z-30 flex h-16 shrink-0 items-center gap-2 border-b backdrop-blur">
      <div className="flex w-full items-center gap-2 px-4">
        <SidebarTrigger className="-ml-1" />
        <Separator orientation="vertical" className="mr-1 data-[orientation=vertical]:h-4" />
        <Pencil className="size-4 shrink-0" />
        <span className="text-sm font-medium">Editing dashboard</span>

        <div className="ml-auto flex items-center gap-2">
          {children}
          <Button variant="outline" size="sm" onClick={onCancel} disabled={saving}>
            Cancel
          </Button>
          <Button size="sm" onClick={onSave} disabled={saving}>
            {saving ? 'Saving...' : 'Save'}
          </Button>
        </div>
      </div>
    </header>
  );
}
