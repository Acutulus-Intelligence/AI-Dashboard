import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import {
  ChevronDown,
  FileUp,
  Folder,
  FolderPlus,
  Globe,
  Loader2,
  Lock,
  Pencil,
  Table2,
  Trash2,
  Sparkles,
  Users,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import type { CompanyRoleResponse } from '@/lib/api/company';
import {
  createCollection,
  deleteCollection,
  deleteCollectionFile,
  getCollection,
  getCollectionFile,
  getCollections,
  updateCollection,
  uploadCollectionFile,
  type CollectionDetailResponse,
  type CollectionFileDetailResponse,
  type CollectionResponse,
  type CollectionVisibility,
} from '../../services/collectionsApi';
import ConfirmDialog from '../components/ConfirmDialog';
import { ROUTES } from '../routes';

interface CollectionsSectionProps {
  canManage: boolean;
  isCompany: boolean;
  roles: CompanyRoleResponse[];
}

interface ExpandedState {
  detail: CollectionDetailResponse;
  shares: Record<string, unknown>;
}

const VISIBILITY_OPTIONS: { value: CollectionVisibility; label: string; hint: string }[] = [
  { value: 'Company', label: 'Entire company', hint: 'Visible to every member' },
  { value: 'Roles', label: 'Specific roles', hint: 'Visible to selected roles only' },
  { value: 'Private', label: 'Only me', hint: 'Visible just to you' },
];

function visibilityLabel(col: CollectionResponse | CollectionDetailResponse, roles: CompanyRoleResponse[]): string {
  if (col.visibility === 'Private') return 'Only me';
  if (col.visibility === 'Company') return 'Company';
  const names = col.allowedRoleIds
    .map((id) => roles.find((r) => r.id === id)?.name)
    .filter(Boolean);
  return names.length > 0 ? `Roles: ${names.join(', ')}` : 'Selected roles';
}

interface VisibilityPickerProps {
  radioGroup: string;
  visibility: CollectionVisibility;
  allowedRoleIds: string[];
  shareableRoles: CompanyRoleResponse[];
  onVisibilityChange: (v: CollectionVisibility) => void;
  onToggleRole: (roleId: string) => void;
}

function VisibilityPicker({
  radioGroup,
  visibility,
  allowedRoleIds,
  shareableRoles,
  onVisibilityChange,
  onToggleRole,
}: VisibilityPickerProps) {
  return (
    <>
      <p className="text-muted-foreground text-sm font-medium">Share with</p>
      <div className="space-y-2">
        {VISIBILITY_OPTIONS.map((opt) => (
          <label
            key={opt.value}
            className={cn(
              'border-border hover:bg-muted/50 flex cursor-pointer items-start gap-3 rounded-lg border p-3 text-sm',
              visibility === opt.value && 'border-brand bg-muted/40',
            )}
          >
            <input
              type="radio"
              name={radioGroup}
              value={opt.value}
              checked={visibility === opt.value}
              onChange={() => onVisibilityChange(opt.value)}
              className="mt-0.5"
            />
            <span>
              <span className="block font-medium">{opt.label}</span>
              <span className="text-muted-foreground block">{opt.hint}</span>
            </span>
          </label>
        ))}
      </div>

      {visibility === 'Roles' && (
        <div className="border-border rounded-lg border p-3">
          <p className="text-muted-foreground mb-2 text-sm font-medium">Select roles</p>
          {shareableRoles.length === 0 ? (
            <p className="text-muted-foreground text-sm">
              No roles available. Create roles under Team members first.
            </p>
          ) : (
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              {shareableRoles.map((role) => (
                <label
                  key={role.id}
                  className="hover:bg-muted flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm"
                >
                  <input
                    type="checkbox"
                    checked={allowedRoleIds.includes(role.id)}
                    onChange={() => onToggleRole(role.id)}
                  />
                  {role.name}
                </label>
              ))}
            </div>
          )}
        </div>
      )}
    </>
  );
}

export default function CollectionsSection({ canManage, isCompany, roles }: CollectionsSectionProps) {
  const navigate = useNavigate();
  const [collections, setCollections] = useState<CollectionResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [showCreate, setShowCreate] = useState(false);
  const [newName, setNewName] = useState('');
  const [newDescription, setNewDescription] = useState('');
  const [newVisibility, setNewVisibility] = useState<CollectionVisibility>('Company');
  const [newAllowedRoleIds, setNewAllowedRoleIds] = useState<string[]>([]);
  const [creating, setCreating] = useState(false);

  const [editingCol, setEditingCol] = useState<CollectionResponse | null>(null);
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editVisibility, setEditVisibility] = useState<CollectionVisibility>('Company');
  const [editAllowedRoleIds, setEditAllowedRoleIds] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);

  const [expanded, setExpanded] = useState<Record<string, ExpandedState>>({});
  const [uploading, setUploading] = useState(false);
  const [uploadTarget, setUploadTarget] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState('');

  const [preview, setPreview] = useState<Record<string, CollectionFileDetailResponse>>({});

  const [deleteTarget, setDeleteTarget] = useState<{ kind: 'collection' | 'file'; id: string; collectionId?: string; name: string } | null>(null);
  const [deleting, setDeleting] = useState(false);

  const refresh = useCallback(async () => {
    try {
      const list = await getCollections();
      setCollections(list);
      setError('');
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load collections.');
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const list = await getCollections();
        if (!cancelled) setCollections(list);
      } catch {
        if (!cancelled) setError('Failed to load collections.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  async function handleCreate() {
    if (!newName.trim()) return;
    if (newVisibility === 'Roles' && newAllowedRoleIds.length === 0) {
      setError('Select at least one role to share this collection with.');
      return;
    }
    setCreating(true);
    setError('');
    try {
      const created = await createCollection({
        name: newName.trim(),
        description: newDescription || null,
        visibility: isCompany ? newVisibility : 'Private',
        allowedRoleIds: newVisibility === 'Roles' ? newAllowedRoleIds : [],
      });
      toast.success(`Collection “${created.name}” created.`);
      setShowCreate(false);
      setNewName('');
      setNewDescription('');
      setNewVisibility('Company');
      setNewAllowedRoleIds([]);
      await refresh();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create collection.');
    } finally {
      setCreating(false);
    }
  }

  function toggleAllowedRole(roleId: string) {
    if (editingCol) {
      setEditAllowedRoleIds((prev) =>
        prev.includes(roleId) ? prev.filter((id) => id !== roleId) : [...prev, roleId],
      );
    } else {
      setNewAllowedRoleIds((prev) =>
        prev.includes(roleId) ? prev.filter((id) => id !== roleId) : [...prev, roleId],
      );
    }
  }

  function startEdit(col: CollectionResponse) {
    setEditingCol(col);
    setEditName(col.name);
    setEditDescription(col.description ?? '');
    setEditVisibility(col.visibility);
    setEditAllowedRoleIds(col.allowedRoleIds);
  }

  function cancelEdit() {
    setEditingCol(null);
    setSaving(false);
  }

  async function handleUpdate() {
    if (!editingCol) return;
    if (!editName.trim()) return;
    if (editVisibility === 'Roles' && editAllowedRoleIds.length === 0) {
      setError('Select at least one role to share this collection with.');
      return;
    }
    setSaving(true);
    setError('');
    try {
      const updated = await updateCollection(editingCol.id, {
        name: editName.trim(),
        description: editDescription || null,
        visibility: isCompany ? editVisibility : 'Private',
        allowedRoleIds: editVisibility === 'Roles' ? editAllowedRoleIds : [],
      });
      toast.success(`Collection “${updated.name}” updated.`);
      setCollections((prev) => prev.map((c) => (c.id === updated.id ? updated : c)));
      setExpanded((prev) => {
        const entry = prev[updated.id];
        if (!entry) return prev;
        return {
          ...prev,
          [updated.id]: {
            ...entry,
            detail: {
              ...entry.detail,
              name: updated.name,
              description: updated.description,
              visibility: updated.visibility,
              allowedRoleIds: updated.allowedRoleIds,
            },
          },
        };
      });
      setEditingCol(null);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update collection.');
    } finally {
      setSaving(false);
    }
  }

  const shareableRoles = roles.filter((r) => !(r.isSystemRole && r.name === 'Owner'));

  const toggleExpand = useCallback(async (id: string) => {
    if (expanded[id]) {
      setExpanded((prev) => {
        const next = { ...prev };
        delete next[id];
        return next;
      });
      return;
    }
    try {
      const detail = await getCollection(id);
      setExpanded((prev) => ({ ...prev, [id]: { detail, shares: {} } }));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load collection.');
    }
  }, [expanded]);

  async function handlePickFile(target: string, next: File | null) {
    if (!next) return;
    if (!next.name.toLowerCase().endsWith('.csv') && !next.name.toLowerCase().endsWith('.xlsx')) {
      setUploadError('Only .csv or .xlsx files are supported.');
      return;
    }
    setUploading(true);
    setUploadTarget(target);
    setUploadError('');
    try {
      const created = await uploadCollectionFile(target, next);
      toast.success(`Uploaded “${created.name}”.`);
      const detail = await getCollection(target);
      setExpanded((prev) => ({ ...prev, [target]: { detail, shares: {} } }));
      await refresh();
    } catch (err: unknown) {
      setUploadError(err instanceof Error ? err.message : 'Upload failed.');
    } finally {
      setUploading(false);
      setUploadTarget(null);
    }
  }

  async function handlePreview(collectionId: string, fileId: string) {
    try {
      const detail = await getCollectionFile(collectionId, fileId);
      setPreview((prev) => ({ ...prev, [fileId]: detail }));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load file preview.');
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    setDeleting(true);
    setError('');
    try {
      if (deleteTarget.kind === 'collection') {
        await deleteCollection(deleteTarget.id);
        setCollections((prev) => prev.filter((c) => c.id !== deleteTarget.id));
        setExpanded((prev) => {
          const next = { ...prev };
          delete next[deleteTarget.id];
          return next;
        });
        toast.success('Collection deleted.');
      } else {
        await deleteCollectionFile(deleteTarget.collectionId!, deleteTarget.id);
        setExpanded((prev) => {
          const entry = prev[deleteTarget.collectionId!];
          if (!entry) return prev;
          return {
            ...prev,
            [deleteTarget.collectionId!]: {
              ...entry,
              detail: {
                ...entry.detail,
                files: entry.detail.files.filter((f) => f.id !== deleteTarget.id),
              },
            },
          };
        });
        toast.success('File deleted.');
      }
      setDeleteTarget(null);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Delete failed.');
    } finally {
      setDeleting(false);
    }
  }

  function openChart(collectionId: string, fileId: string) {
    navigate(ROUTES.GRAPHS_NEW, {
      state: { collectionId, fileId, fromConnections: true },
    });
  }

  return (
    <div className="space-y-4">
      {error && (
        <div className="border-destructive/40 bg-destructive/10 text-destructive rounded-lg border px-3 py-2 text-sm">
          {error}
        </div>
      )}

      {showCreate && (
        <Card>
          <CardHeader>
            <CardTitle>New data collection</CardTitle>
            <CardDescription>
              Files uploaded to a collection are shared with the whole company (or kept private for
              individual accounts).
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <Input
              placeholder="Collection name"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              maxLength={200}
            />
            <Input
              placeholder="Description (optional)"
              value={newDescription}
              onChange={(e) => setNewDescription(e.target.value)}
              maxLength={500}
            />
            {isCompany && (
              <VisibilityPicker
                radioGroup="new-visibility"
                visibility={newVisibility}
                allowedRoleIds={newAllowedRoleIds}
                shareableRoles={shareableRoles}
                onVisibilityChange={setNewVisibility}
                onToggleRole={toggleAllowedRole}
              />
            )}
            <div className="flex gap-2">
              <Button onClick={() => void handleCreate()} disabled={creating || !newName.trim()}>
                {creating && <Loader2 className="animate-spin" />}
                Create
              </Button>
              <Button variant="outline" onClick={() => setShowCreate(false)}>
                Cancel
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {canManage && (
        <Button variant="outline" onClick={() => setShowCreate((v) => !v)}>
          <FolderPlus />
          Create collection
        </Button>
      )}

      {loading && <p className="text-muted-foreground text-sm">Loading collections…</p>}

      {!loading &&
        collections.map((col) => {
          const state = expanded[col.id];
          const open = !!state;
          const editing = editingCol?.id === col.id;
          return (
            <div key={col.id} className="space-y-3">
              {editing && (
                <Card>
                  <CardHeader>
                    <CardTitle>Edit collection</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    <Input
                      placeholder="Collection name"
                      value={editName}
                      onChange={(e) => setEditName(e.target.value)}
                      maxLength={200}
                    />
                    <Input
                      placeholder="Description (optional)"
                      value={editDescription}
                      onChange={(e) => setEditDescription(e.target.value)}
                      maxLength={500}
                    />
                    {isCompany && (
                      <VisibilityPicker
                        radioGroup={`edit-visibility-${col.id}`}
                        visibility={editVisibility}
                        allowedRoleIds={editAllowedRoleIds}
                        shareableRoles={shareableRoles}
                        onVisibilityChange={setEditVisibility}
                        onToggleRole={toggleAllowedRole}
                      />
                    )}
                    <div className="flex gap-2">
                      <Button
                        onClick={() => void handleUpdate()}
                        disabled={saving || !editName.trim()}
                      >
                        {saving && <Loader2 className="animate-spin" />}
                        Save changes
                      </Button>
                      <Button variant="outline" onClick={cancelEdit} disabled={saving}>
                        Cancel
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              )}
            <div className="border-border bg-card rounded-xl border">
              <div className="flex items-stretch">
                <button
                  type="button"
                  onClick={() => void toggleExpand(col.id)}
                  aria-expanded={open}
                  className="hover:bg-muted/50 flex min-w-0 flex-1 cursor-pointer items-center justify-between gap-3 rounded-l-xl p-4 text-left transition-colors"
                >
                  <div className="flex min-w-0 items-center gap-3">
                    <Folder className="text-brand size-5 shrink-0" />
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                        <span className="font-semibold">{col.name}</span>
                        <span
                          className="text-muted-foreground inline-flex items-center gap-1 text-xs"
                          title={visibilityLabel(col, roles)}
                        >
                          {col.visibility === 'Private' ? (
                            <Lock className="size-3" />
                          ) : col.visibility === 'Company' ? (
                            <Globe className="size-3" />
                          ) : (
                            <Users className="size-3" />
                          )}
                          {visibilityLabel(col, roles)}
                        </span>
                        {col.description && (
                          <span className="text-muted-foreground text-sm">{col.description}</span>
                        )}
                      </div>
                      <p className="text-muted-foreground text-sm">
                        {col.fileCount} files · {col.rowCount.toLocaleString()} rows ·{' '}
                        {new Date(col.createdAt).toLocaleDateString()}
                      </p>
                    </div>
                  </div>
                  <ChevronDown
                    className={cn('text-muted-foreground size-4 shrink-0 transition-transform', open && 'rotate-180')}
                  />
                </button>
                {canManage && (
                  <button
                    type="button"
                    onClick={() => startEdit(col)}
                    aria-label={`Edit ${col.name}`}
                    className="text-muted-foreground hover:bg-muted hover:text-foreground flex w-12 cursor-pointer items-center justify-center border-l transition-colors"
                  >
                    <Pencil className="size-4" />
                  </button>
                )}
                {canManage && (
                  <button
                    type="button"
                    onClick={() => setDeleteTarget({ kind: 'collection', id: col.id, name: col.name })}
                    aria-label={`Delete ${col.name}`}
                    className="text-muted-foreground hover:bg-muted hover:text-destructive flex w-12 cursor-pointer items-center justify-center rounded-r-xl border-l transition-colors"
                  >
                    <Trash2 className="size-4" />
                  </button>
                )}
              </div>

              {open && (
                <div className="border-border border-t p-4">
                  <div className="mb-3 flex items-center justify-between gap-2">
                    <p className="text-muted-foreground text-sm">
                      Upload files to this collection. Each file becomes a chart source.
                    </p>
                    {canManage && (
                      <label className="border-input hover:bg-muted inline-flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-1.5 text-sm">
                        <FileUp className="size-4" />
                        {uploading && uploadTarget === col.id ? 'Uploading…' : 'Upload file'}
                        <input
                          type="file"
                          accept=".csv,.xlsx,text/csv"
                          className="hidden"
                          onChange={(e) => void handlePickFile(col.id, e.target.files?.[0] ?? null)}
                        />
                      </label>
                    )}
                  </div>
                  {uploadError && uploadTarget === col.id && (
                    <div className="bg-destructive/10 text-destructive mb-2 rounded-lg px-3 py-2 text-sm">
                      {uploadError}
                    </div>
                  )}

                  {state.detail.files.length === 0 && (
                    <p className="text-muted-foreground text-sm">No files uploaded yet.</p>
                  )}

                  <div className="space-y-1">
                    {state.detail.files.map((file) => {
                      const previewDetail = preview[file.id];
                      return (
                        <div key={file.id} className="border-border rounded-lg border">
                          <div className="flex items-center justify-between gap-3 px-3 py-2.5">
                            <button
                              type="button"
                              onClick={() => void handlePreview(col.id, file.id)}
                              className="hover:bg-muted flex min-w-0 flex-1 cursor-pointer items-center gap-3 rounded-lg px-2 py-1 text-left text-sm"
                            >
                              <Table2 className="text-brand size-4 shrink-0" />
                              <span className="min-w-0">
                                <span className="block truncate font-medium">{file.name}</span>
                                <span className="text-muted-foreground block text-xs">
                                  {file.columnCount} columns · {file.rowCount.toLocaleString()} rows
                                </span>
                              </span>
                            </button>
                            <div className="flex shrink-0 items-center gap-1">
                              <Button
                                type="button"
                                size="sm"
                                variant="outline"
                                onClick={() => openChart(col.id, file.id)}
                              >
                                <Sparkles />
                                Chart
                              </Button>
                              {canManage && (
                                <Button
                                  type="button"
                                  size="sm"
                                  variant="ghost"
                                  className="text-red-500"
                                  onClick={() =>
                                    setDeleteTarget({
                                      kind: 'file',
                                      id: file.id,
                                      collectionId: col.id,
                                      name: file.name,
                                    })
                                  }
                                  aria-label={`Delete ${file.name}`}
                                >
                                  <Trash2 />
                                </Button>
                              )}
                            </div>
                          </div>

                          {previewDetail && (
                            <div className="border-border border-t px-3 py-3">
                              <div className="mb-2 flex items-center justify-between">
                                <p className="text-muted-foreground text-sm">
                                  Preview — {previewDetail.columns.length} columns
                                </p>
                                <button
                                  type="button"
                                  className="text-muted-foreground hover:text-foreground cursor-pointer"
                                  onClick={() =>
                                    setPreview((prev) => {
                                      const next = { ...prev };
                                      delete next[file.id];
                                      return next;
                                    })
                                  }
                                  aria-label="Close preview"
                                >
                                  <X className="size-4" />
                                </button>
                              </div>
                              <div className="max-h-72 overflow-auto">
                                <table className="w-full text-left text-sm">
                                  <thead>
                                    <tr className="text-muted-foreground border-b">
                                      {previewDetail.columns.map((col) => (
                                        <th key={col.name} className="sticky top-0 z-10 bg-card pb-1.5 pr-3 font-medium">
                                          <span className="block truncate">{col.name}</span>
                                          <span className="block text-[10px] font-normal">{col.type}</span>
                                        </th>
                                      ))}
                                    </tr>
                                  </thead>
                                  <tbody>
                                    {previewDetail.previewRows.map((row, i) => (
                                      <tr key={i} className="border-b last:border-0">
                                        {previewDetail.columns.map((col) => (
                                          <td key={col.name} className="text-muted-foreground max-w-40 truncate py-1.5 pr-3">
                                            {String(row[col.name] ?? '')}
                                          </td>
                                        ))}
                                      </tr>
                                    ))}
                                  </tbody>
                                </table>
                              </div>
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>
            </div>
          );
        })}

      {!loading && collections.length === 0 && (
        <div className="border-border text-muted-foreground rounded-xl border border-dashed p-12 text-center">
          <Folder className="mx-auto mb-3 size-10 opacity-40" />
          <p className="text-sm">
            {canManage
              ? 'No data collections yet. Create one to start uploading files.'
              : 'No data collections yet. Ask an admin to create one.'}
          </p>
        </div>
      )}

      <ConfirmDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => {
          if (!open && !deleting) setDeleteTarget(null);
        }}
        title={deleteTarget?.kind === 'collection' ? 'Delete collection?' : 'Delete file?'}
        description={
          deleteTarget?.kind === 'collection'
            ? `This permanently removes “${deleteTarget?.name}” and all its files. Charts that use it will keep their last data but stop refreshing.`
            : `This permanently removes “${deleteTarget?.name}”. Charts that use it will keep their last data but stop refreshing.`
        }
        confirmLabel="Delete"
        variant="destructive"
        loading={deleting}
        onConfirm={() => void confirmDelete()}
      />
    </div>
  );
}