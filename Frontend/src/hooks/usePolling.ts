import { useEffect, useRef } from 'react';

interface UsePollingOptions {
  intervalMs?: number;
  onPoll: () => void | Promise<void>;
  enabled?: boolean;
}

export function usePolling({ intervalMs = 30_000, onPoll, enabled = true }: UsePollingOptions) {
  const onPollRef = useRef(onPoll);

  useEffect(() => {
    onPollRef.current = onPoll;
  }, [onPoll]);

  useEffect(() => {
    if (!enabled) return;

    const poll = () => {
      void onPollRef.current();
    };

    const interval = window.setInterval(poll, intervalMs);
    const handleFocus = () => {
      if (document.visibilityState === 'visible') poll();
    };
    window.addEventListener('focus', handleFocus);
    document.addEventListener('visibilitychange', handleFocus);

    return () => {
      window.clearInterval(interval);
      window.removeEventListener('focus', handleFocus);
      document.removeEventListener('visibilitychange', handleFocus);
    };
  }, [intervalMs, enabled]);
}
