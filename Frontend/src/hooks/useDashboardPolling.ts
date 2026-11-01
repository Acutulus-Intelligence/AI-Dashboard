import { useCallback, useEffect, useRef, useState } from 'react';

export const DASHBOARD_REFRESH_INTERVAL_MS = 5 * 60 * 1000;

export interface DashboardRefreshOutcome {
  succeeded: number;
  failed: number;
  lastUpdatedAt: number | null;
  /** True when polling should stop (for example an expired subscription). */
  blocked?: boolean;
}

interface UseDashboardPollingOptions {
  enabled: boolean;
  intervalMs?: number;
  /** Manual refreshes should refresh every chart; automatic polls may skip opted-out charts. */
  onRefresh: (manual: boolean) => Promise<DashboardRefreshOutcome>;
}

interface UseDashboardPollingResult {
  isRefreshing: boolean;
  lastUpdatedAt: number | null;
  nextRefreshAt: number | null;
  error: string | null;
  refreshNow: () => Promise<void>;
  markUpdated: (timestamp?: number) => void;
  markError: (message: string | null) => void;
}

const REFRESH_ERROR_MESSAGE = 'Retrying connection… Showing the last known data.';

export function useDashboardPolling({
  enabled,
  intervalMs = DASHBOARD_REFRESH_INTERVAL_MS,
  onRefresh,
}: UseDashboardPollingOptions): UseDashboardPollingResult {
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<number | null>(null);
  const [nextRefreshAt, setNextRefreshAt] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const onRefreshRef = useRef(onRefresh);
  const enabledRef = useRef(enabled);
  const intervalRef = useRef(intervalMs);
  const mountedRef = useRef(true);
  const blockedRef = useRef(false);
  const timerRef = useRef<number | null>(null);
  const inFlightRef = useRef<Promise<void> | null>(null);
  const lastUpdatedRef = useRef<number | null>(null);
  const scheduleRef = useRef<(delay: number) => void>(() => {});
  const runRefreshRef = useRef<(manual: boolean) => Promise<void>>(async () => {});

  useEffect(() => {
    onRefreshRef.current = onRefresh;
  }, [onRefresh]);

  useEffect(() => {
    enabledRef.current = enabled;
  }, [enabled]);

  useEffect(() => {
    intervalRef.current = intervalMs;
  }, [intervalMs]);

  const clearTimer = useCallback(() => {
    if (timerRef.current !== null) {
      window.clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  const clearSchedule = useCallback(() => {
    clearTimer();
    setNextRefreshAt(null);
  }, [clearTimer]);

  const schedule = useCallback((delay: number) => {
    clearTimer();
    if (!enabledRef.current || blockedRef.current) {
      setNextRefreshAt(null);
      return;
    }

    const safeDelay = Math.max(0, delay);
    setNextRefreshAt(Date.now() + safeDelay);
    timerRef.current = window.setTimeout(() => {
      timerRef.current = null;
      void runRefreshRef.current(false);
    }, safeDelay);
  }, [clearTimer]);

  useEffect(() => {
    scheduleRef.current = schedule;
  }, [schedule]);

  const runRefresh = useCallback(async (manual: boolean) => {
    if (inFlightRef.current) {
      await inFlightRef.current;
      return;
    }

    clearTimer();
    setNextRefreshAt(null);

    const run = (async () => {
      if (mountedRef.current) {
        setIsRefreshing(true);
      }

      try {
        const outcome = await onRefreshRef.current(manual);
        if (!mountedRef.current) return;

        if (outcome.succeeded > 0) {
          const updatedAt = outcome.lastUpdatedAt ?? Date.now();
          lastUpdatedRef.current = updatedAt;
          setLastUpdatedAt(updatedAt);
        }

        blockedRef.current = outcome.blocked === true;

        if (outcome.failed > 0) {
          setError(REFRESH_ERROR_MESSAGE);
        } else if (outcome.succeeded > 0) {
          setError(null);
        }
      } catch {
        if (mountedRef.current) {
          setError(REFRESH_ERROR_MESSAGE);
        }
      } finally {
        if (mountedRef.current) {
          setIsRefreshing(false);
        }
        inFlightRef.current = null;

        const shouldContinue =
          manual || enabledRef.current;
        if (
          shouldContinue &&
          enabledRef.current &&
          !blockedRef.current &&
          mountedRef.current &&
          document.visibilityState === 'visible'
        ) {
          scheduleRef.current(intervalRef.current);
        } else if (!enabledRef.current || blockedRef.current) {
          setNextRefreshAt(null);
        }
      }
    })();

    inFlightRef.current = run;
    await run;
  }, [clearTimer]);

  useEffect(() => {
    runRefreshRef.current = runRefresh;
  }, [runRefresh]);

  const refreshNow = useCallback(async () => {
    await runRefreshRef.current(true);
  }, []);

  const markUpdated = useCallback((timestamp = Date.now()) => {
    lastUpdatedRef.current = timestamp;
    setLastUpdatedAt(timestamp);
  }, []);

  const markError = useCallback((message: string | null) => {
    setError(message);
  }, []);

  useEffect(() => {
    if (!enabled) {
      clearTimer();
      return;
    }

    if (document.visibilityState === 'visible' && !blockedRef.current) {
      schedule(intervalMs);
    }

    return clearTimer;
  }, [enabled, intervalMs, schedule, clearTimer]);

  useEffect(() => {
    const handleVisibilityChange = () => {
      if (document.visibilityState !== 'visible') {
        clearSchedule();
        return;
      }

      if (!enabledRef.current || blockedRef.current) {
        return;
      }

      const last = lastUpdatedRef.current;
      if (last === null || Date.now() - last >= intervalRef.current) {
        void runRefreshRef.current(false);
      } else {
        scheduleRef.current(intervalRef.current - (Date.now() - last));
      }
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
  }, [clearSchedule]);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      clearTimer();
      inFlightRef.current = null;
    };
  }, [clearTimer]);

  return {
    isRefreshing,
    lastUpdatedAt,
    nextRefreshAt: enabled ? nextRefreshAt : null,
    error,
    refreshNow,
    markUpdated,
    markError,
  };
}
