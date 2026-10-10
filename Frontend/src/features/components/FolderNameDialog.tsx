import { useState } from 'react';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';

interface FolderNameDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  submitLabel: string;
  initialName?: string;
  loading?: boolean;
  onSubmit: (name: string) => void | Promise<void>;
}

interface FolderNameFormProps {
  initialName: string;
  submitLabel: string;
  loading: boolean;
  onSubmit: (name: string) => void | Promise<void>;
  onCancel: () => void;
}

function FolderNameForm({
  initialName,
  submitLabel,
  loading,
  onSubmit,
  onCancel,
}: FolderNameFormProps) {
  const [name, setName] = useState(initialName);

  function handleSubmit() {
    const trimmed = name.trim();
    if (!trimmed) return;
    void onSubmit(trimmed);
  }

  return (
    <>
      <div className="grid gap-2 py-1">
        <Input
          autoFocus
          value={name}
          onChange={(e) => setName(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              handleSubmit();
            }
          }}
          placeholder="Folder name"
          maxLength={200}
          aria-label="Folder name"
        />
      </div>
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onCancel} disabled={loading}>
          Cancel
        </Button>
        <Button type="button" onClick={handleSubmit} disabled={loading || !name.trim()}>
          {loading && <Loader2 className="animate-spin" />}
          {submitLabel}
        </Button>
      </DialogFooter>
    </>
  );
}

export default function FolderNameDialog({
  open,
  onOpenChange,
  title,
  submitLabel,
  initialName = '',
  loading = false,
  onSubmit,
}: FolderNameDialogProps) {
  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!loading) onOpenChange(next);
      }}
    >
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>
        <FolderNameForm
          initialName={initialName}
          submitLabel={submitLabel}
          loading={loading}
          onSubmit={onSubmit}
          onCancel={() => onOpenChange(false)}
        />
      </DialogContent>
    </Dialog>
  );
}
