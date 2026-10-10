import { Folder, FolderPlus, Inbox, Layers, Pencil, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { ChartFolderResponse } from '../../lib/api/chartFolders';

export const ALL_FOLDER = 'all';
export const UNFILED_FOLDER = 'unfiled';

interface ChartFolderSidebarProps {
  folders: ChartFolderResponse[];
  counts: Map<string, number>;
  totalCount: number;
  unfiledCount: number;
  selected: string;
  onSelect: (value: string) => void;
  onCreate: () => void;
  onRename: (folder: ChartFolderResponse) => void;
  onDelete: (folder: ChartFolderResponse) => void;
  className?: string;
}

interface RowProps {
  icon: typeof Folder;
  label: string;
  count: number;
  selected: boolean;
  onSelect: () => void;
  onRename?: () => void;
  onDelete?: () => void;
}

function Row({ icon: Icon, label, count, selected, onSelect, onRename, onDelete }: RowProps) {
  return (
    <div
      className={cn(
        'group flex items-center rounded-lg',
        selected ? 'bg-muted' : 'hover:bg-muted/60',
      )}
    >
      <button
        type="button"
        onClick={onSelect}
        aria-current={selected ? 'true' : undefined}
        className="flex min-w-0 flex-1 cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors"
      >
        <Icon
          className={cn(
            'size-4 shrink-0',
            selected ? 'text-brand' : 'text-muted-foreground',
          )}
        />
        <span className="min-w-0 flex-1 truncate">{label}</span>
        <span className="text-muted-foreground text-xs tabular-nums">{count}</span>
      </button>
      {onRename && onDelete && (
        <div className="flex items-center pr-1 opacity-100 transition-opacity focus-within:opacity-100 sm:opacity-0 sm:group-hover:opacity-100 sm:focus-within:opacity-100">
          <button
            type="button"
            onClick={onRename}
            aria-label={`Rename ${label}`}
            className="text-muted-foreground hover:text-foreground flex size-6 cursor-pointer items-center justify-center rounded-md transition-colors"
          >
            <Pencil className="size-3.5" />
          </button>
          <button
            type="button"
            onClick={onDelete}
            aria-label={`Delete ${label}`}
            className="text-muted-foreground hover:text-destructive flex size-6 cursor-pointer items-center justify-center rounded-md transition-colors"
          >
            <Trash2 className="size-3.5" />
          </button>
        </div>
      )}
    </div>
  );
}

export default function ChartFolderSidebar({
  folders,
  counts,
  totalCount,
  unfiledCount,
  selected,
  onSelect,
  onCreate,
  onRename,
  onDelete,
  className,
}: ChartFolderSidebarProps) {
  return (
    <aside className={cn('space-y-1', className)}>
      <div className="flex items-center justify-between px-2 pb-1">
        <span className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
          Folders
        </span>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-7"
          onClick={onCreate}
          aria-label="New folder"
        >
          <FolderPlus className="size-4" />
        </Button>
      </div>

      <Row
        icon={Layers}
        label="All"
        count={totalCount}
        selected={selected === ALL_FOLDER}
        onSelect={() => onSelect(ALL_FOLDER)}
      />
      <Row
        icon={Inbox}
        label="Unfiled"
        count={unfiledCount}
        selected={selected === UNFILED_FOLDER}
        onSelect={() => onSelect(UNFILED_FOLDER)}
      />

      {folders.map((folder) => (
        <Row
          key={folder.id}
          icon={Folder}
          label={folder.name}
          count={counts.get(folder.id) ?? 0}
          selected={selected === folder.id}
          onSelect={() => onSelect(folder.id)}
          onRename={() => onRename(folder)}
          onDelete={() => onDelete(folder)}
        />
      ))}

      {folders.length === 0 && (
        <p className="text-muted-foreground px-2 pt-1 text-xs">
          No folders yet. Create one to group charts.
        </p>
      )}
    </aside>
  );
}
