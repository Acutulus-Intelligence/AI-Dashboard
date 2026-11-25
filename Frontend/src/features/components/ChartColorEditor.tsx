import { useMemo, useState } from 'react';
import { Check, Pipette, RotateCcw, Shuffle } from 'lucide-react';
import ColorPicker from '@rc-component/color-picker';
import type { Color } from '@rc-component/color-picker';
import '@rc-component/color-picker/assets/index.css';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import {
  DEFAULT_COMPANY_COLORS,
  colorLabel,
  cssColorToHex,
  isChartToken,
  isHexColor,
} from '../charts/companyColors';
import {
  PALETTES,
  THEME_CHART_COLORS,
  distinctChartColor,
  getPalette,
  resolveColorSlots,
  type Palette,
} from '../charts/palette';
import type { ChartStyleConfig } from '../charts/types';

interface ChartColorEditorProps {
  value: ChartStyleConfig;
  onChange: (next: ChartStyleConfig) => void;
  companyColors?: string[];
  colorSlots: number;
  colorLabels?: string[];
  /** Stable keys for colorsByKey; defaults to the labels. */
  colorKeys?: string[];
  colorAxis?: 'series' | 'category';
  /** Keep the list short with its own scrollbar (dashboard popover). */
  scrollable?: boolean;
  className?: string;
}

function toSolidHex(color: string): string {
  const hex = cssColorToHex(color);
  return isHexColor(hex) ? hex.slice(0, 7).toLowerCase() : '#808080';
}

function SlotColorPicker({
  color,
  slotLabel,
  onPick,
}: {
  color: string;
  slotLabel: string;
  onPick: (hex: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState(toSolidHex(color));

  function applyDraft() {
    if (isHexColor(draft)) {
      onPick(draft.slice(0, 7).toLowerCase());
    }
  }

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        if (next) setDraft(toSolidHex(color));
        setOpen(next);
      }}
    >
      <PopoverTrigger asChild>
        <button
          type="button"
          aria-label={`Custom colour for ${slotLabel}`}
          title={`Custom colour for ${slotLabel}`}
          className="border-border hover:bg-muted flex h-6 cursor-pointer items-center gap-1 rounded-full border px-2 text-[11px] transition-colors"
        >
          <span
            className="border-border size-3 rounded-full border"
            style={{ background: color }}
          />
          <Pipette className="size-3" />
          Custom
        </button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-auto gap-2 p-3">
        <ColorPicker
          value={draft}
          disabledAlpha
          onChange={(next: Color) => setDraft(toSolidHex(next.toHexString()))}
          onChangeComplete={(next: Color) => onPick(toSolidHex(next.toHexString()))}
        />
        <div className="flex items-center gap-2">
          <Input
            value={draft}
            maxLength={7}
            aria-label={`Hex colour for ${slotLabel}`}
            className="h-8 w-28 font-mono text-xs"
            onChange={(event) => setDraft(event.target.value)}
            onBlur={applyDraft}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                event.preventDefault();
                applyDraft();
              }
            }}
          />
          <Button type="button" size="icon-sm" variant="outline" onClick={applyDraft}>
            <Check className="size-3.5" />
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}

function PaletteButton({
  palette,
  active,
  onSelect,
}: {
  palette: Palette;
  active: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={cn(
        'flex cursor-pointer items-center gap-2 rounded-lg border px-2.5 py-1.5 text-xs transition-colors',
        active ? 'border-primary bg-primary/5' : 'border-border hover:bg-muted',
      )}
    >
      <span className="flex gap-0.5">
        {palette.colors.slice(0, 4).map((color, index) => (
          <span
            key={index}
            className="size-3 rounded-full border border-black/10"
            style={{ background: color }}
          />
        ))}
      </span>
      {palette.label}
    </button>
  );
}

export default function ChartColorEditor({
  value,
  onChange,
  companyColors,
  colorSlots,
  colorLabels,
  colorKeys,
  colorAxis = 'series',
  scrollable = false,
  className,
}: ChartColorEditorProps) {
  const swatches = useMemo(
    () =>
      Array.from(
        new Set([
          ...(companyColors && companyColors.length > 0
            ? companyColors
            : DEFAULT_COMPANY_COLORS),
          ...THEME_CHART_COLORS,
        ]),
      ),
    [companyColors],
  );
  const swatchEntries = useMemo(
    () => swatches.map((color) => ({ color, label: colorLabel(color) })),
    [swatches],
  );
  const palette = getPalette(value.palette);
  const slots = Math.max(1, colorSlots);

  const labels = useMemo(
    () =>
      Array.from({ length: slots }, (_, index) => {
        const label = colorLabels?.[index]?.trim();
        if (label) return label;
        return colorAxis === 'category' ? `Category ${index + 1}` : `Series ${index + 1}`;
      }),
    [colorAxis, colorLabels, slots],
  );

  const keys = useMemo(
    () =>
      Array.from({ length: slots }, (_, index) => {
        if (colorKeys) return colorKeys[index]?.trim() ?? '';
        return colorLabels?.[index]?.trim() ?? '';
      }),
    [colorKeys, colorLabels, slots],
  );

  const effective = useMemo(
    () => resolveColorSlots(value, keys),
    [keys, value],
  );

  const duplicates = useMemo(() => {
    const seen = new Map<string, number>();
    for (const color of effective) {
      const key = colorLabel(color);
      seen.set(key, (seen.get(key) ?? 0) + 1);
    }
    return [...seen.values()].some((count) => count > 1);
  }, [effective]);

  function patch(partial: Partial<ChartStyleConfig>) {
    onChange({ ...value, ...partial });
  }

  function setSlotColor(index: number, color: string) {
    const key = keys[index];
    if (key) {
      // Keep the base palette for the other slots; keyed overrides win.
      patch({
        colorsByKey: { ...value.colorsByKey, [key]: color },
        colors: value.colors ? [...value.colors] : undefined,
      });
      return;
    }

    const nextColors = effective.map((current) =>
      isHexColor(current) || isChartToken(current) ? current : cssColorToHex(current),
    );
    nextColors[index] = color;
    patch({ colors: nextColors, palette: undefined });
  }

  function resetSlot(index: number) {
    const colors = value.colors ? [...value.colors] : [];
    colors[index] = '';
    while (colors.length > 0 && !colors[colors.length - 1]) colors.pop();

    const key = keys[index];
    const colorsByKey = { ...value.colorsByKey };
    if (key) delete colorsByKey[key];

    patch({
      colors: colors.length > 0 ? colors : undefined,
      colorsByKey: Object.keys(colorsByKey).length > 0 ? colorsByKey : undefined,
    });
  }

  function resetAll() {
    patch({ colors: undefined, colorsByKey: undefined, palette: value.palette ?? 'default' });
  }

  function distinctColors() {
    const next = Array.from({ length: slots }, (_, index) => distinctChartColor(index));
    const colorsByKey = { ...value.colorsByKey };
    keys.forEach((key, index) => {
      if (key) colorsByKey[key] = next[index];
    });
    patch({
      colors: next,
      colorsByKey: Object.keys(colorsByKey).length > 0 ? colorsByKey : undefined,
      palette: undefined,
    });
  }

  return (
    <div className={cn('flex flex-col gap-4', className)}>
      <div className="grid gap-2">
        <div className="flex items-center justify-between gap-2">
          <Label>Palette</Label>
          <div className="flex items-center gap-1">
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              title="Give every slot a distinct colour"
              aria-label="Give every slot a distinct colour"
              onClick={distinctColors}
            >
              <Shuffle className="size-3.5" />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              title="Reset colours"
              aria-label="Reset colours"
              onClick={resetAll}
            >
              <RotateCcw className="size-3.5" />
            </Button>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          {PALETTES.map((entry) => (
            <PaletteButton
              key={entry.id}
              palette={entry}
              active={palette.id === entry.id && !value.colors?.length}
              onSelect={() =>
                patch({ palette: entry.id, colors: undefined, colorsByKey: undefined })
              }
            />
          ))}
        </div>
      </div>

      <div className="grid gap-2">
        <Label>
          {colorAxis === 'category' ? 'Category colours' : 'Series colours'}
          <span className="text-muted-foreground ml-1 font-normal">({slots})</span>
        </Label>
        <div
          className={cn(
            'grid gap-1.5 pr-1',
            scrollable && 'max-h-72 overflow-y-auto',
          )}
        >
          {labels.map((label, index) => {
            const current = effective[index];
            return (
              <div key={`${label}-${index}`} className="flex items-center gap-2">
                <span className="text-muted-foreground w-24 shrink-0 truncate text-xs" title={label}>
                  {label}
                </span>
                <div className="flex min-w-0 flex-1 flex-wrap items-center gap-1">
                  {swatchEntries.map(({ color: swatch, label: swatchLabel }) => (
                    <button
                      key={swatch}
                      type="button"
                      aria-label={`Colour ${swatchLabel} for ${label}`}
                      title={swatchLabel}
                      onClick={() => setSlotColor(index, swatch)}
                      className={cn(
                        'size-5 cursor-pointer rounded-full border border-black/10 transition-transform',
                        current === swatch && 'ring-ring scale-110 ring-2',
                      )}
                      style={{ background: swatch }}
                    />
                  ))}
                  <SlotColorPicker
                    color={current}
                    slotLabel={label}
                    onPick={(hex) => setSlotColor(index, hex)}
                  />
                  <button
                    type="button"
                    aria-label={`Reset colour for ${label}`}
                    title={`Reset colour for ${label}`}
                    onClick={() => resetSlot(index)}
                    className="text-muted-foreground hover:text-foreground ml-0.5 flex size-5 cursor-pointer items-center justify-center rounded-full"
                  >
                    <RotateCcw className="size-3" />
                  </button>
                </div>
              </div>
            );
          })}
        </div>
        {duplicates && (
          <p className="text-amber-600 text-xs">
            Two or more slots share the same colour. Pick a different shade so they stay readable.
          </p>
        )}
      </div>
    </div>
  );
}
