import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import type { ChartFolderResponse } from '../../lib/api/chartFolders';

const NO_FOLDER = '__none__';

interface FolderSelectProps {
  folders: ChartFolderResponse[];
  value: string | null;
  onChange: (value: string | null) => void;
  id?: string;
  className?: string;
}

export default function FolderSelect({
  folders,
  value,
  onChange,
  id,
  className,
}: FolderSelectProps) {
  return (
    <Select
      value={value ?? NO_FOLDER}
      onValueChange={(next) => onChange(next === NO_FOLDER ? null : next)}
    >
      <SelectTrigger id={id} className={className}>
        <SelectValue placeholder="No folder" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={NO_FOLDER}>No folder</SelectItem>
        {folders.map((folder) => (
          <SelectItem key={folder.id} value={folder.id}>
            {folder.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
