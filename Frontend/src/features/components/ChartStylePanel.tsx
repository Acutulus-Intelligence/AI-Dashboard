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
import ChartColorEditor from './ChartColorEditor';
import {
  resolveStyleCapabilities,
  type ChartDescriptor,
  type ChartStyleConfig,
  type DecimalMode,
  type NumberNotation,
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
  /** Stable keys for colorsByKey (empty for unnamed/duplicate labels). */
  colorKeys?: string[];
  /** Whether the slots represent series or categories. */
  colorAxis?: 'series' | 'category';
  /** Number of datasets; category colour mode needs a single series. */
  seriesCount?: number;
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

export default function ChartStylePanel({
  descriptor,
  value,
  onChange,
  companyColors,
  colorSlots = 2,
  colorLabels,
  colorKeys,
  colorAxis,
  seriesCount = 1,
  className,
}: ChartStylePanelProps) {
  const variant = value.variant ?? descriptor.variants[0]?.id;
  const caps = resolveStyleCapabilities(descriptor);
  const decimalsText =
    typeof value.decimals === 'number' && Number.isFinite(value.decimals)
      ? String(value.decimals)
      : '';
  const decimalMode: DecimalMode = value.decimalMode === 'truncate' ? 'truncate' : 'round';
  const notation: NumberNotation =
    value.notation === 'full' || value.notation === 'compact' ? value.notation : 'auto';
  const showValueLabels = caps.valueLabels;
  const showColors = caps.colors && colorSlots > 0;
  const showInfo = caps.info;
  const showLabelsBlock = showValueLabels || showInfo;
  const colorBySpec = descriptor.colorAxisParam
    ? descriptor.params.find((spec) => spec.key === descriptor.colorAxisParam)
    : undefined;
  const otherParams = descriptor.params.filter(
    (spec) => spec.key !== descriptor.colorAxisParam,
  );
  const colorByValue = String(
    value.params?.[descriptor.colorAxisParam ?? ''] ?? colorBySpec?.default ?? 'series',
  );
  const categoryColorsDisabled = seriesCount > 1 || variant === 'stacked';
  const hasVariantSection = descriptor.variants.length > 1;
  const hasColorBySection = Boolean(colorBySpec);
  const hasColorSection = caps.colors;
  const hasParamsSection = otherParams.length > 0;
  const showLabelsSeparator =
    showLabelsBlock &&
    (hasVariantSection || hasColorBySection || hasColorSection || hasParamsSection);
  const showParamsSeparator =
    hasParamsSection && (hasVariantSection || hasColorBySection || hasColorSection);

  function patch(partial: Partial<ChartStyleConfig>) {
    onChange({ ...value, ...partial });
  }

  function setParam(key: string, next: unknown) {
    patch({ params: { ...value.params, [key]: next } });
  }

  function setColorBy(key: string, next: string) {
    // Positional colours belong to the old axis; keyed overrides stay and the
    // palette remains as the fallback for the new axis.
    patch({ params: { ...value.params, [key]: next }, colors: undefined });
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

              <div className="grid gap-1.5">
                <Label htmlFor="value-notation" className="text-muted-foreground text-xs font-normal">
                  Number format
                </Label>
                <Select
                  value={notation}
                  onValueChange={(next) => patch({ notation: next as NumberNotation })}
                >
                  <SelectTrigger id="value-notation" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="auto">Auto (compact above 10k)</SelectItem>
                    <SelectItem value="full">Full numbers</SelectItem>
                    <SelectItem value="compact">Compact (42k, 1.2M)</SelectItem>
                  </SelectContent>
                </Select>
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

      {colorBySpec && descriptor.colorAxisParam && (
        <div className="grid gap-2">
          <Label>Color by</Label>
          <ToggleGroup
            type="single"
            variant="outline"
            value={categoryColorsDisabled ? 'series' : colorByValue}
            onValueChange={(next) => next && setColorBy(descriptor.colorAxisParam!, next)}
            className="flex flex-wrap justify-start"
          >
            <ToggleGroupItem value="series" className="px-3 text-xs">
              Series
            </ToggleGroupItem>
            <ToggleGroupItem
              value="category"
              className="px-3 text-xs"
              disabled={categoryColorsDisabled}
            >
              Category
            </ToggleGroupItem>
          </ToggleGroup>
          <p className="text-muted-foreground text-xs">
            {categoryColorsDisabled
              ? seriesCount > 1
                ? 'Category colours need a single series.'
                : 'Category colours are not available for stacked bars.'
              : colorBySpec.help}
          </p>
        </div>
      )}

      {showColors && (
        <ChartColorEditor
          value={value}
          onChange={onChange}
          companyColors={companyColors}
          colorSlots={colorSlots}
          colorLabels={colorLabels}
          colorKeys={colorKeys}
          colorAxis={colorAxis}
        />
      )}

      {hasParamsSection && (
        <>
          {showParamsSeparator && <Separator />}
          <div className="grid gap-4">
            {otherParams.map((spec) => (
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
