import { ChevronDown, LayoutDashboard, LineChart, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

interface CreateDropdownProps {
  onNewChart: () => void;
  onNewDashboard: () => void;
}

export default function CreateDropdown({ onNewChart, onNewDashboard }: CreateDropdownProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button size="sm">
          <Plus />
          <span className="hidden sm:inline">Create</span>
          <ChevronDown className="opacity-70" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-48">
        <DropdownMenuItem onSelect={onNewChart}>
          <LineChart />
          New chart
        </DropdownMenuItem>
        <DropdownMenuItem onSelect={onNewDashboard}>
          <LayoutDashboard />
          New dashboard
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
