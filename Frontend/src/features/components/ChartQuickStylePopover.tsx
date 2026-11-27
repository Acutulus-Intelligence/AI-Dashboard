import { useEffect, useMemo, useRef, useState } from 'react';
import { Loader2, Palette } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { updateChartStyle } from '../../lib/api/charts';
import { resolveColorAxis } from '../charts/colorAxis';
import { getPalette, resolveStyle } from '../charts/palette';
import { get } from '../charts/registry';
import { shapeForDisplay } from '../charts/shape';
import { resolveStyleCapabilities, type ChartData, type ChartStyleConfig } from '../charts/types';
import ChartColorEditor from './ChartColorEditor';

interface ChartQuickStylePopoverProps {
  chartId: string;
  chartType: string;
  data?: ChartData;
  styleConfig?: ChartStyleConfig;
  onSaved: (style: ChartStyleConfig | null) => void;
  /** Apply the active palette to every chart on the dashboard. */
  onApplyPalette?: (palette: string) => Promise<void> | void;
}

const EMPTY_DATA: ChartData = { labels: [], datasets: [] };

/** Ignores the backend-managed schema version when comparing style states. */
function serializeStyle(style: ChartStyleConfig | null | undefined): string {
  if (!style) return '{}';
  const rest = { ...style };
  delete rest.schemaVersion;
  return JSON.stringify(rest);
}

export default function ChartQuickStylePopover({
  chartId,
  chartType,
  data,
  styleConfig,
  onSaved,
  onApplyPalette,
}: ChartQuickStylePopoverProps) {
  const descriptor = get(chartType);
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<ChartStyleConfig>(styleConfig ?? {});
  const [saving, setSaving] = useState(false);
  const [applying, setApplying] = useState(false);
  const lastSavedRef = useRef(serializeStyle(styleConfig));
  const savedCallbackRef = useRef(onSaved);
  savedCallbackRef.current = onSaved;
  const saveChainRef = useRef<Promise<void>>(Promise.resolve());
  const latestSaveRef = useRef(0);

  const colorView = useMemo(() => {
    if (!descriptor) return null;
    const chartData = data ?? EMPTY_DATA;
    const rawAxis = resolveColorAxis(descriptor, chartData, draft);
    const preliminary = resolveStyle(descriptor, draft, rawAxis.keys);
    const shaped = shapeForDisplay(chartData, preliminary);
    const axis = resolveColorAxis(descriptor, shaped, draft);
    return {
      slots: Math.max(axis.slots.length, 1),
      labels: axis.slots.map((slot) => slot.label),
      keys: axis.slots.map((slot) => slot.key),
      axis: axis.axis,
      seriesCount: shaped.datasets.length,
    };
  }, [data, descriptor, draft]);

  useEffect(() => {
    if (!open) return;
    const serialized = serializeStyle(draft);
    if (serialized === lastSavedRef.current) return;

    const timer = window.setTimeout(() => {
      void save(draft);
    }, 450);

    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft, open]);

  function save(next: ChartStyleConfig): Promise<void> {
    const requestId = ++latestSaveRef.current;

    const run = async () => {
      // A newer edit was queued while this one waited: skip the stale state.
      if (requestId !== latestSaveRef.current) return;

      const serialized = serializeStyle(next);
      if (serialized === lastSavedRef.current) return;

      setSaving(true);
      try {
        const result = await updateChartStyle(chartId, next);
        lastSavedRef.current = serializeStyle(result.styleConfig);
        savedCallbackRef.current(result.styleConfig ?? null);
      } catch (error: unknown) {
        toast.error(error instanceof Error ? error.message : 'Could not save chart colours.');
      } finally {
        setSaving(false);
      }
    };

    // Serialize writes: network responses must not land out of order.
    const chained = saveChainRef.current.then(run, run);
    saveChainRef.current = chained;
    return chained;
  }

  async function applyPaletteToAll() {
    if (!onApplyPalette || applying) return;
    const palette = getPalette(draft.palette).id;
    setApplying(true);
    try {
      await onApplyPalette(palette);
    } finally {
      setApplying(false);
    }
  }

  function handleOpenChange(next: boolean) {
    if (next) {
      setDraft(styleConfig ?? {});
      lastSavedRef.current = serializeStyle(styleConfig);
    } else {
      void save(draft);
    }
    setOpen(next);
  }

  if (!descriptor) return null;

  const caps = resolveStyleCapabilities(descriptor);
  if (!caps.colors) return null;

  const colorByKey = descriptor.colorAxisParam;
  const categoryMode = colorByKey
    ? draft.params?.[colorByKey] === 'category'
    : descriptor.colorAxis === 'category';
  const canColorByCategory =
    Boolean(colorByKey) && colorView !== null && colorView.seriesCount <= 1 && draft.variant !== 'stacked';

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        <button
          type="button"
          className="text-muted-foreground hover:text-foreground nodrag inline-flex size-7 cursor-pointer items-center justify-center rounded-md"
          aria-label="Edit chart colours"
          title="Edit chart colours"
          onMouseDown={(event) => event.stopPropagation()}
          onClick={(event) => event.stopPropagation()}
        >
          {saving ? <Loader2 className="size-3.5 animate-spin" /> : <Palette className="size-3.5" />}
        </button>
      </PopoverTrigger>
      <PopoverContent
        align="end"
        className="w-80 max-w-[calc(100vw-2rem)] gap-3 p-3"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="flex items-center justify-between gap-2">
          <div className="min-w-0">
            <p className="truncate text-sm font-medium">Chart colours</p>
            <p className="text-muted-foreground truncate text-xs">{descriptor.label}</p>
          </div>
          {saving && <span className="text-muted-foreground text-xs">Saving…</span>}
        </div>

        {canColorByCategory && colorByKey && (
          <div className="flex items-center justify-between gap-3 rounded-lg border px-2.5 py-2">
            <div className="min-w-0">
              <Label htmlFor="quick-color-by" className="text-xs">
                Colour each category
              </Label>
              <p className="text-muted-foreground text-[11px]">One colour per bar.</p>
            </div>
            <Switch
              id="quick-color-by"
              checked={categoryMode}
              onCheckedChange={(checked) =>
                setDraft((prev) => ({
                  ...prev,
                  colors: undefined,
                  params: { ...prev.params, [colorByKey]: checked ? 'category' : 'series' },
                }))
              }
            />
          </div>
        )}

        {colorView && (
          <ChartColorEditor
            value={draft}
            onChange={setDraft}
            colorSlots={colorView.slots}
            colorLabels={colorView.labels}
            colorKeys={colorView.keys}
            colorAxis={colorView.axis}
            scrollable
          />
        )}

        {onApplyPalette && (
          <Button
            type="button"
            variant="secondary"
            className="w-full"
            disabled={applying}
            onClick={() => void applyPaletteToAll()}
          >
            {applying && <Loader2 className="animate-spin" />}
            Apply palette to all charts
          </Button>
        )}

        <Button type="button" variant="outline" className="w-full" onClick={() => void save(draft)}>
          Save colours
        </Button>
      </PopoverContent>
    </Popover>
  );
}
