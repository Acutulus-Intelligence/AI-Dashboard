import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Loader2, Palette, Plus, RotateCcw, Trash2 } from 'lucide-react';
import ColorPicker from '@rc-component/color-picker';
import type { Color } from '@rc-component/color-picker';
import '@rc-component/color-picker/assets/index.css';
import { toast } from 'sonner';
import AppShell from '../layouts/AppShell';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import * as companyApi from '../../lib/api/company';
import { useAuth } from '../store/useAuth';
import { ROUTES } from '../routes';
import {
  DEFAULT_COMPANY_COLORS,
  MAX_COMPANY_COLORS,
  colorLabel,
} from '../charts/companyColors';

function toSolidHex(color: Color): string {
  return color.toHexString().slice(0, 7).toLowerCase();
}

function ColorSwatchEditor({
  color,
  onChange,
  onRemove,
  canRemove,
}: {
  color: string;
  onChange: (next: string) => void;
  onRemove: () => void;
  canRemove: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState(color.startsWith('#') ? color : '#808080');

  return (
    <div className="group relative flex flex-col items-center gap-2">
      <Popover
        open={open}
        onOpenChange={(next) => {
          if (next) setDraft(color.startsWith('#') ? color : '#808080');
          setOpen(next);
        }}
      >
        <PopoverTrigger asChild>
          <button
            type="button"
            title={`Edit ${colorLabel(color)}`}
            aria-label={`Edit colour ${colorLabel(color)}`}
            className="border-border hover:ring-ring size-14 cursor-pointer rounded-full border shadow-sm transition hover:ring-2"
            style={{ background: color }}
          />
        </PopoverTrigger>
        <PopoverContent align="center" className="w-auto gap-2 p-3">
          <ColorPicker
            value={draft}
            disabledAlpha
            onChange={(c) => setDraft(toSolidHex(c))}
            onChangeComplete={(c) => onChange(toSolidHex(c))}
          />
          <p className="text-muted-foreground font-mono text-xs tabular-nums">{draft}</p>
        </PopoverContent>
      </Popover>
      <span className="text-muted-foreground max-w-20 truncate text-center text-[11px]">
        {colorLabel(color)}
      </span>
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        disabled={!canRemove}
        className="opacity-0 transition-opacity group-hover:opacity-100"
        aria-label={`Remove ${colorLabel(color)}`}
        onClick={onRemove}
      >
        <Trash2 className="size-3.5" />
      </Button>
    </div>
  );
}

function AddColorButton({
  disabled,
  onAdd,
}: {
  disabled?: boolean;
  onAdd: (hex: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState('#808080');

  return (
    <div className="flex flex-col items-center gap-2">
      <Popover
        open={open}
        onOpenChange={(next) => {
          if (next) setDraft('#808080');
          setOpen(next);
        }}
      >
        <PopoverTrigger asChild>
          <button
            type="button"
            disabled={disabled}
            title="Add colour"
            aria-label="Add colour"
            className={cn(
              'border-foreground/30 text-foreground/70 hover:border-foreground/50 flex size-14 cursor-pointer items-center justify-center rounded-full border border-dashed transition',
              'disabled:cursor-not-allowed disabled:opacity-40',
            )}
          >
            <Plus className="size-5" />
          </button>
        </PopoverTrigger>
        <PopoverContent align="center" className="w-auto gap-2 p-3">
          <ColorPicker
            value={draft}
            disabledAlpha
            onChange={(c) => setDraft(toSolidHex(c))}
            onChangeComplete={(c) => {
              onAdd(toSolidHex(c));
              setOpen(false);
            }}
          />
          <p className="text-muted-foreground font-mono text-xs tabular-nums">{draft}</p>
        </PopoverContent>
      </Popover>
      <span className="text-muted-foreground text-[11px]">Add</span>
      <span className="size-7" aria-hidden />
    </div>
  );
}

export default function AdminStylePage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const isOwner = user?.companyRoleName === 'Owner';

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [colors, setColors] = useState<string[]>([...DEFAULT_COMPANY_COLORS]);
  const [savedSnapshot, setSavedSnapshot] = useState<string[]>([...DEFAULT_COMPANY_COLORS]);

  const isDirty = useMemo(
    () => JSON.stringify(colors) !== JSON.stringify(savedSnapshot),
    [colors, savedSnapshot],
  );

  useEffect(() => {
    if (!isOwner) {
      navigate(ROUTES.ADMIN, { replace: true });
      return;
    }

    let cancelled = false;
    setLoading(true);
    companyApi
      .getCompanyStyle()
      .then((res) => {
        if (cancelled) return;
        const next = res.colors?.length ? res.colors : [...DEFAULT_COMPANY_COLORS];
        setColors(next);
        setSavedSnapshot(next);
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load company style.');
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [isOwner, navigate]);

  function updateAt(index: number, hex: string) {
    setColors((prev) => {
      const next = [...prev];
      if (next.includes(hex) && next[index] !== hex) {
        toast.error('That colour is already in the palette.');
        return prev;
      }
      next[index] = hex;
      return next;
    });
  }

  function removeAt(index: number) {
    setColors((prev) => {
      if (prev.length <= 1) {
        toast.error('Keep at least one colour.');
        return prev;
      }
      return prev.filter((_, i) => i !== index);
    });
  }

  function addColor(hex: string) {
    setColors((prev) => {
      if (prev.length >= MAX_COMPANY_COLORS) {
        toast.error(`Maximum ${MAX_COMPANY_COLORS} colours.`);
        return prev;
      }
      if (prev.includes(hex)) {
        toast.error('That colour is already in the palette.');
        return prev;
      }
      return [...prev, hex];
    });
  }

  function resetDefaults() {
    setColors([...DEFAULT_COMPANY_COLORS]);
  }

  function cancelEdits() {
    setColors([...savedSnapshot]);
  }

  async function save() {
    if (colors.length < 1) {
      toast.error('Keep at least one colour.');
      return;
    }
    setSaving(true);
    setError('');
    try {
      const res = await companyApi.updateCompanyStyle({ colors });
      const next = res.colors?.length ? res.colors : [...DEFAULT_COMPANY_COLORS];
      setColors(next);
      setSavedSnapshot(next);
      toast.success('Style saved.');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to save style.';
      setError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <AppShell
      breadcrumbs={[
        { label: 'Administration', to: ROUTES.ADMIN },
        { label: 'Style' },
      ]}
    >
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Style</h1>
          <p className="text-muted-foreground text-sm">
            Define how charts look across your company — colours, appearance and visual style.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="outline" onClick={resetDefaults} disabled={loading || saving}>
            <RotateCcw />
            Reset defaults
          </Button>
          {isDirty && (
            <>
              <Button type="button" variant="outline" onClick={cancelEdits} disabled={saving}>
                Cancel
              </Button>
              <Button type="button" onClick={() => void save()} disabled={saving}>
                {saving && <Loader2 className="animate-spin" />}
                Save changes
              </Button>
            </>
          )}
        </div>
      </div>

      {error && (
        <div className="border-destructive/40 bg-destructive/10 text-destructive rounded-lg border px-3 py-2 text-sm">
          {error}
        </div>
      )}

      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <div className="bg-primary/10 text-primary flex size-10 items-center justify-center rounded-xl">
              <Palette className="size-5" />
            </div>
            <div>
              <CardTitle>Appearance</CardTitle>
              <CardDescription>
                Adjust the shared look for your charts. Edit a swatch, add your own, or remove ones you do not need.
              </CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-muted-foreground flex min-h-40 items-center justify-center text-sm">
              Loading appearance…
            </div>
          ) : (
            <div className="flex flex-wrap gap-6">
              {colors.map((color, index) => (
                <ColorSwatchEditor
                  key={`${color}-${index}`}
                  color={color}
                  canRemove={colors.length > 1}
                  onChange={(next) => updateAt(index, next)}
                  onRemove={() => removeAt(index)}
                />
              ))}
              <AddColorButton
                disabled={colors.length >= MAX_COMPANY_COLORS}
                onAdd={addColor}
              />
            </div>
          )}
        </CardContent>
      </Card>
    </AppShell>
  );
}
