import { useEffect, useState } from 'react';
import { AlertCircle, RefreshCw, SlidersHorizontal } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Popover,
  PopoverContent,
  PopoverDescription,
  PopoverHeader,
  PopoverTitle,
  PopoverTrigger,
} from '@/components/ui/popover';
import { Switch } from '@/components/ui/switch';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { DASHBOARD_REFRESH_INTERVAL_MS } from '../../hooks/useDashboardPolling';

export interface RefreshChartOption {
  id: string;
  title: string;
  autoRefreshEnabled: boolean | null;
}

interface DashboardRefreshBarProps {
  enabled: boolean;
  canManageSettings: boolean;
  settingsSaving: boolean;
  isRefreshing: boolean;
  lastUpdatedAt: number | null;
  nextRefreshAt: number | null;
  error: string | null;
  charts: RefreshChartOption[];
  onRefresh: () => void;
  onToggleEnabled: (enabled: boolean) => void;
  onToggleChart: (widgetId: string, enabled: boolean) => void;
}

function formatRelativeTime(timestamp: number, now: number): string {
  const seconds = Math.max(0, Math.floor((now - timestamp) / 1000));
  if (seconds < 10) return 'just now';
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

function formatCountdown(milliseconds: number): string {
  const seconds = Math.max(0, Math.ceil(milliseconds / 1000));
  const minutes = Math.floor(seconds / 60);
  return `${minutes}:${String(seconds % 60).padStart(2, '0')}`;
}

export default function DashboardRefreshBar({
  enabled,
  canManageSettings,
  settingsSaving,
  isRefreshing,
  lastUpdatedAt,
  nextRefreshAt,
  error,
  charts,
  onRefresh,
  onToggleEnabled,
  onToggleChart,
}: DashboardRefreshBarProps) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!nextRefreshAt && !lastUpdatedAt) return;
    const timer = window.setInterval(
      () => setNow(Date.now()),
      nextRefreshAt ? 1000 : 30_000,
    );
    return () => window.clearInterval(timer);
  }, [nextRefreshAt, lastUpdatedAt]);

  const lastUpdatedLabel = lastUpdatedAt
    ? `Last updated ${formatRelativeTime(lastUpdatedAt, now)}`
    : 'Not refreshed yet';

  const countdownLabel =
    enabled && nextRefreshAt
      ? `Refreshing in ${formatCountdown(nextRefreshAt - now)}`
      : null;

  const elapsedSinceUpdate = lastUpdatedAt ? Math.max(0, now - lastUpdatedAt) : 0;
  const progress = lastUpdatedAt
    ? Math.min(100, (elapsedSinceUpdate / DASHBOARD_REFRESH_INTERVAL_MS) * 100)
    : 0;

  const hasCharts = charts.length > 0;

  return (
    <div className="border-border bg-card/60 rounded-xl border px-3 py-2">
      <div className="flex flex-wrap items-center gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={onRefresh}
          disabled={isRefreshing || !hasCharts}
        >
          <RefreshCw className={isRefreshing ? 'animate-spin' : undefined} />
          {isRefreshing ? 'Refreshing…' : 'Refresh'}
        </Button>

        <div className="text-muted-foreground flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-xs" role="status" aria-live="polite">
          <span>{lastUpdatedLabel}</span>
          {countdownLabel && (
            <>
              <span aria-hidden="true">·</span>
              <span>{countdownLabel}</span>
            </>
          )}
          {!enabled && (
            <>
              <span aria-hidden="true">·</span>
              <span>Auto-refresh off</span>
            </>
          )}
        </div>

        <div className="ml-auto flex items-center gap-1">
          <Popover>
            <PopoverTrigger asChild>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                aria-label="Auto-refresh settings"
                title="Auto-refresh settings"
              >
                <SlidersHorizontal />
              </Button>
            </PopoverTrigger>
            <PopoverContent align="end" className="w-80 overflow-x-hidden">
              <PopoverHeader>
                <PopoverTitle>Auto-refresh</PopoverTitle>
                <PopoverDescription>
                  Keep dashboard data fresh without reloading the page.
                </PopoverDescription>
              </PopoverHeader>

              <div className="flex items-start justify-between gap-3 rounded-lg border p-2.5">
                <div className="min-w-0">
                  <p className="text-sm font-medium">Every 5 minutes</p>
                  <p className="text-muted-foreground text-xs">
                    Charts with a failing query keep their last good data.
                  </p>
                </div>
                <Switch
                  checked={enabled}
                  disabled={!canManageSettings || settingsSaving || !hasCharts}
                  onCheckedChange={onToggleEnabled}
                  aria-label="Enable auto-refresh for this dashboard"
                />
              </div>

              {hasCharts && (
                <div className="space-y-1.5">
                  <p className="text-muted-foreground px-0.5 text-xs font-medium uppercase tracking-wide">
                    Charts
                  </p>
                  <div className="max-h-56 space-y-1 overflow-x-hidden overflow-y-auto">
                    {charts.map((chart) => {
                      const chartEnabled = enabled && chart.autoRefreshEnabled !== false;
                      return (
                        <div
                          key={chart.id}
                          className="flex min-w-0 items-center justify-between gap-3 overflow-x-hidden rounded-md px-1.5 py-1.5 hover:bg-muted/60"
                        >
                          <span className="min-w-0 flex-1 truncate text-sm" title={chart.title}>
                            {chart.title}
                          </span>
                          <Switch
                            size="sm"
                            checked={chartEnabled}
                            disabled={!canManageSettings || !enabled || settingsSaving}
                            onCheckedChange={(checked) => onToggleChart(chart.id, checked)}
                            aria-label={`Auto-refresh ${chart.title}`}
                          />
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}

              {!canManageSettings && (
                <p className="text-muted-foreground text-xs">
                  Only the company owner can change refresh settings.
                </p>
              )}
              {canManageSettings && !enabled && hasCharts && (
                <p className="text-muted-foreground text-xs">
                  Turn on auto-refresh to choose which charts poll.
                </p>
              )}
            </PopoverContent>
          </Popover>
        </div>
      </div>

      {enabled && nextRefreshAt && (
        <div
          className="bg-muted mt-2 h-0.5 w-full overflow-hidden rounded-full"
          aria-hidden="true"
        >
          <div
            className="bg-primary/60 h-full rounded-full transition-[width] duration-1000 ease-linear"
            style={{ width: `${progress}%` }}
          />
        </div>
      )}

      {error && (
        <div className="mt-2 flex items-center gap-2 rounded-md border border-amber-300/70 bg-amber-50 px-2.5 py-1.5 text-xs text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-200">
          <AlertCircle size={14} className="shrink-0" />
          <span className="min-w-0 flex-1">{error}</span>
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                type="button"
                variant="ghost"
                size="xs"
                onClick={onRefresh}
                disabled={isRefreshing}
              >
                Retry now
              </Button>
            </TooltipTrigger>
            <TooltipContent>Refresh this dashboard again</TooltipContent>
          </Tooltip>
        </div>
      )}
    </div>
  );
}
