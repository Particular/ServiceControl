import { watch, ref, shallowReadonly, type WatchStopHandle } from "vue";
import { useDocumentVisibility, useTimeoutPoll } from "@vueuse/core";

export default function createAutoRefresh(name: string, fetch: () => Promise<void>, intervalMs: number) {
  let refCount = 0;
  let watchStop: WatchStopHandle | null = null;
  const interval = ref(intervalMs);
  const isRefreshing = ref(false);
  const fetchWrapper = async () => {
    isRefreshing.value = true;
    await fetch();
    isRefreshing.value = false;
  };
  const { pause, resume } = useTimeoutPoll(
    fetchWrapper,
    interval,
    { immediate: false, immediateCallback: true } // we control first fetch manually
  );

  const visibility = useDocumentVisibility();

  const start = async () => {
    refCount++;
    if (refCount === 1) {
      console.debug(`[AutoRefresh] Starting auto-refresh for ${name} every ${interval.value}ms`);
      resume();
      watchStop = watch(visibility, (current, previous) => {
        if (current === "visible" && previous === "hidden") {
          console.debug(`[AutoRefresh] Resuming auto-refresh for ${name} as document became visible`);
          resume();
        }

        if (current === "hidden" && previous === "visible") {
          console.debug(`[AutoRefresh] Pausing auto-refresh for ${name} as document became hidden`);
          pause();
        }
      });
    } else {
      console.debug(`[AutoRefresh] Incremented refCount for ${name} to ${refCount}`);
      // Because another component has started using the auto-refresh, do an immediate refresh to ensure it has up-to-date data
      await fetchWrapper();
    }
  };

  const stop = () => {
    refCount--;
    if (refCount <= 0) {
      console.debug(`[AutoRefresh] Stopping auto-refresh for ${name}`);
      pause();
      watchStop?.();
      watchStop = null;
      refCount = 0;
    } else {
      console.debug(`[AutoRefresh] Decremented refCount for ${name} to ${refCount}`);
    }
  };

  const updateInterval = (newIntervalMs: number) => {
    interval.value = newIntervalMs;
  };

  return { refreshNow: fetchWrapper, isRefreshing: shallowReadonly(isRefreshing), updateInterval, start, stop };
}
