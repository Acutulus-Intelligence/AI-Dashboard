import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Slider } from '@/components/ui/slider';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';
import { PALETTES } from '../charts/palette';
import { DEFAULT_COMPANY_COLORS, colorLabel } from '../charts/companyColors';
import {
  resolveStyleCapabilities,
  type ChartDescriptor,
  type ChartStyleConfig,
  type DecimalMode,
  type ParamSpec,
} from '../charts/types';

interface ChartStylePanelProps {
  descriptor: ChartDescriptor;
  value: ChartStyleConfig;
  onChange: (next: ChartStyleConfig) => void;
  /** Company (or default) swatches shown in the series/slice colour picker. */
  companyColors?: string[];
  /** How many colour slots to offer (series or pie slices). */
  colorSlots?: number;
  /** Optional labels for colour slots (e.g. pie category names). */
  colorLabels?: string[];
  className?: string;
}

const MAX_DECIMALS = 10;

function ParamControl({
  spec,
  value,
  onChange,
}: {
  spec: ParamSpec;
  value: unknown;
  onChange: (next: unknown) => void;
}) {
  if (spec.kind === 'boolean') {
    return (
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <Label htmlFor={spec.key}>{spec.label}</Label>
          {spec.help && <p className="text-muted-foreground text-xs">{spec.help}</p>}
        </div>
        <Switch
          id={spec.key}
          checked={Boolean(value ?? spec.default)}
          onCheckedChange={onChange}
        />
      </div>
    );
  }

  if (spec.kind === 'number') {
    const current = typeof value === 'number' ? value : spec.default;
    return (
      <div className="grid gap-2">
        <div className="flex items-center justify-between gap-2">
          <Label htmlFor={spec.key}>{spec.label}</Label>
          <span className="text-muted-foreground font-mono text-xs tabular-nums">{current}</span>
        </div>
        <Slider
          id={spec.key}
          min={spec.min}
          max={spec.max}
          step={spec.step}
          value={[current]}
          onValueChange={([next]) => onChange(next)}
        />
        {spec.help && <p className="text-muted-foreground text-xs">{spec.help}</p>}
      </div>
    );
  }

  return (
    <div className="grid gap-2">
      <Label htmlFor={spec.key}>{spec.label}</Label>
      <Select value={String(value ?? spec.default)} onValueChange={onChange}>
        <SelectTrigger id={spec.key} className="w-full">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {spec.options.map((opt) => (
            <SelectItem key={opt.value} value={opt.value}>
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {spec.help && <p className="text-muted-foreground text-xs">{spec.help}</p>}
    </div>
  );
}

function applySeriesColor(
  colors: string[] | undefined,
  colorSlots: number,
  seriesIndex: number,
  hex: string,
): string[] | undefined {
  const next = Array.from(
    { length: Math.max(colorSlots, colors?.length ?? 0, seriesIndex + 1) },
    (_, i) => (i === seriesIndex ? hex : (colors?.[i] ?? '')),
  );
  while (next.length > 0 && !next[next.length - 1]) next.pop();
  return next.length ? next : undefined;
}

export default function ChartStylePanel({
  descriptor,
  value,
  onChange,
  companyColors,
  colorSlots = 2,
  colorLabels,
  className,
}: ChartStylePanelProps) {
  const variant = value.variant ?? descriptor.variants[0]?.id;
  const palette = value.palette ?? 'default';
  const caps = resolveStyleCapabilities(descriptor);
  const swatches =
    companyColors && companyColors.length > 0 ? companyColors : DEFAULT_COMPANY_COLORS;
  const decimalsText =
    typeof value.decimals === 'number' && Number.isFinite(value.decimals)
      ? String(value.decimals)
      : '';
  const decimalMode: DecimalMode = value.decimalMode === 'truncate' ? 'truncate' : 'round';
  const showValueLabels = caps.valueLabels;
  const showColors = caps.colors && colorSlots > 0;
  const showInfo = caps.info;
  const showLabelsBlock = showValueLabels || showInfo;
  const hasVariantSection = descriptor.variants.length > 1;
  const hasColorSection = caps.colors;
  const hasParamsSection = descriptor.params.length > 0;
  const showLabelsSeparator = showLabelsBlock && (hasVariantSection || hasColorSection || hasParamsSection);
  const showParamsSeparator = hasParamsSection && (hasVariantSection || hasColorSection);

  function patch(partial: Partial<ChartStyleConfig>) {
    onChange({ ...value, ...partial, customColors: undefined });
  }

  function setParam(key: string, next: unknown) {
    patch({ params: { ...value.params, [key]: next } });
  }

  function setColor(index: number, color: string) {
    patch({
      colors: applySeriesColor(value.colors, colorSlots, index, color),
      palette: undefined,
    });
  }

  function setDecimalsFromInput(raw: string) {
    const trimmed = raw.trim();
    if (trimmed === '') {
      patch({ decimals: undefined, decimalMode: undefined });
      return;
    }
    const parsed = Number(trimmed);
    if (!Number.isFinite(parsed)) return;
    const clamped = Math.min(MAX_DECIMALS, Math.max(0, Math.trunc(parsed)));
    patch({
      decimals: clamped,
      decimalMode: value.decimalMode === 'truncate' ? 'truncate' : 'round',
    });
  }

  return (
    <div className={cn('flex flex-col gap-5', className)}>
      {showLabelsBlock && (
        <div className="grid gap-3">
          <Label>Labels</Label>
          {showValueLabels && (
            <>
              <div className="grid grid-cols-2 gap-2">
                <div className="grid gap-1.5">
                  <Label htmlFor="value-prefix" className="text-muted-foreground text-xs font-normal">
                    Prefix
                  </Label>
                  <Input
                    id="value-prefix"
                    value={value.valuePrefix ?? ''}
                    maxLength={16}
                    placeholder="e.g. $"
                    onChange={(e) => patch({ valuePrefix: e.target.value || undefined })}
                  />
                </div>
                <div className="grid gap-1.5">
                  <Label htmlFor="value-suffix" className="text-muted-foreground text-xs font-normal">
                    Suffix
                  </Label>
                  <Input
                    id="value-suffix"
                    value={value.valueSuffix ?? ''}
                    maxLength={16}
                    placeholder="e.g. %"
                    onChange={(e) => patch({ valueSuffix: e.target.value || undefined })}
                  />
                </div>
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="value-decimals" className="text-muted-foreground text-xs font-normal">
                  Decimals
                </Label>
                <Input
                  id="value-decimals"
                  inputMode="numeric"
                  min={0}
                  max={MAX_DECIMALS}
                  value={decimalsText}
                  placeholder="All (default)"
                  onChange={(e) => setDecimalsFromInput(e.target.value)}
                />
                {decimalsText !== '' && (
                  <ToggleGroup
                    type="single"
                    variant="outline"
                    size="sm"
                    value={decimalMode}
                    onValueChange={(next) => {
                      if (next === 'round' || next === 'truncate') patch({ decimalMode: next });
                    }}
                    className="justify-start"
                  >
                    <ToggleGroupItem value="round" className="px-3 text-xs">
                      Round
                    </ToggleGroupItem>
                    <ToggleGroupItem value="truncate" className="px-3 text-xs">
                      Truncate
                    </ToggleGroupItem>
                  </ToggleGroup>
                )}
              </div>
            </>
          )}

          {showInfo && (
            <div className="grid gap-1.5">
              <Label htmlFor="chart-info" className="text-muted-foreground text-xs font-normal">
                Info tooltip
              </Label>
              <Textarea
                id="chart-info"
                value={value.info ?? ''}
                maxLength={500}
                rows={2}
                placeholder="Short note shown when hovering the info icon…"
                onChange={(e) => patch({ info: e.target.value || undefined })}
              />
            </div>
          )}
        </div>
      )}

      {showLabelsSeparator && <Separator />}

      {descriptor.variants.length > 1 && (
        <div className="grid gap-2">
          <Label>Variant</Label>
          <ToggleGroup
            type="single"
            variant="outline"
            value={variant}
            onValueChange={(next) => next && patch({ variant: next })}
            className="flex flex-wrap justify-start"
          >
            {descriptor.variants.map((v) => (
              <ToggleGroupItem key={v.id} value={v.id} className="px-3 text-xs">
                {v.label}
              </ToggleGroupItem>
            ))}
          </ToggleGroup>
          <p className="text-muted-foreground text-xs">
            {descriptor.variants.find((v) => v.id === variant)?.description}
          </p>
        </div>
      )}

      {caps.colors && (
        <div className="grid gap-2">
          <Label>Palette</Label>
          <div className="flex flex-wrap gap-2">
            {PALETTES.map((p) => (
              <button
                key={p.id}
                type="button"
                onClick={() => patch({ palette: p.id, colors: undefined })}
                className={cn(
                  'flex cursor-pointer items-center gap-2 rounded-lg border px-2.5 py-1.5 text-xs transition-colors',
                  palette === p.id && !value.colors?.length
                    ? 'border-primary bg-primary/5'
                    : 'border-border hover:bg-muted',
                )}
              >
                <span className="flex gap-0.5">
                  {p.colors.slice(0, 4).map((c, i) => (
                    <span
                      key={i}
                      className="size-3 rounded-full border border-black/10"
                      style={{ background: c }}
                    />
                  ))}
                </span>
                {p.label}
              </button>
            ))}
          </div>
        </div>
      )}

      {showColors && (
        <div className="grid gap-2">
          <Label>{colorLabels?.length ? 'Slice colours' : 'Series colours'}</Label>
          <div className="grid gap-2">
            {Array.from({ length: colorSlots }, (_, i) => {
              const current = value.colors?.[i];
              const slotLabel = colorLabels?.[i]?.trim() || `Series ${i + 1}`;
              return (
                <div key={i} className="flex items-center gap-2">
                  <span
                    className="text-muted-foreground w-24 shrink-0 truncate text-xs"
                    title={slotLabel}
                  >
                    {slotLabel}
                  </span>
                  <div className="flex flex-wrap gap-1">
                    {swatches.map((swatch) => (
                      <button
                        key={swatch}
                        type="button"
                        aria-label={`Colour ${colorLabel(swatch)} for ${slotLabel}`}
                        title={colorLabel(swatch)}
                        onClick={() => setColor(i, swatch)}
                        className={cn(
                          'size-5 cursor-pointer rounded-full border border-black/10 transition-transform',
                          current === swatch && 'ring-ring scale-110 ring-2',
                        )}
                        style={{ background: swatch }}
                      />
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {hasParamsSection && (
        <>
          {showParamsSeparator && <Separator />}
          <div className="grid gap-4">
            {descriptor.params.map((spec) => (
              <ParamControl
                key={spec.key}
                spec={spec}
                value={value.params?.[spec.key]}
                onChange={(next) => setParam(spec.key, next)}
              />
            ))}
          </div>
        </>
      )}
    </div>
  );
}
