import { useEffect, useRef } from 'react';
import {
  getSessionExpirationReason,
  isUserIdleForPolling,
  recordUserActivity,
} from '../services/sessionManager';

interface UseIdleAwarePollingOptions {
  enabled?: boolean;
  immediate?: boolean;
  idlePauseMs?: number;
}

export function useIdleAwarePolling(
  callback: () => void | Promise<void>,
  intervalMs: number,
  options: UseIdleAwarePollingOptions = {}
): void {
  const { enabled = true, immediate = false, idlePauseMs } = options;
  const callbackRef = useRef(callback);
  const wasPausedRef = useRef(false);

  useEffect(() => {
    callbackRef.current = callback;
  });

  useEffect(() => {
    if (!enabled) return;

    const runIfActive = () => {
      if (getSessionExpirationReason() !== null || isUserIdleForPolling(idlePauseMs)) {
        wasPausedRef.current = true;
        return;
      }
      wasPausedRef.current = false;
      void callbackRef.current();
    };

    if (immediate && getSessionExpirationReason() === null) {
      void callbackRef.current();
    }

    const intervalId = window.setInterval(runIfActive, intervalMs);

    const onResumeCandidate = (event: Event) => {
      if (document.hidden || !wasPausedRef.current) return;
      if (event.type === 'pointerdown' || event.type === 'keydown') {
        const activityResult = recordUserActivity(false);
        if (activityResult.expiredReason) return;
      }
      if (getSessionExpirationReason() !== null || isUserIdleForPolling(idlePauseMs)) return;
      wasPausedRef.current = false;
      void callbackRef.current();
    };

    document.addEventListener('visibilitychange', onResumeCandidate);
    window.addEventListener('pointerdown', onResumeCandidate, { passive: true });
    window.addEventListener('keydown', onResumeCandidate, { passive: true });

    return () => {
      window.clearInterval(intervalId);
      document.removeEventListener('visibilitychange', onResumeCandidate);
      window.removeEventListener('pointerdown', onResumeCandidate);
      window.removeEventListener('keydown', onResumeCandidate);
    };
  }, [enabled, immediate, idlePauseMs, intervalMs]);
}
