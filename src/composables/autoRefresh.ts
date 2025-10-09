import { onMounted, onUnmounted, watch, ref, type WatchStopHandle } from "vue";
import { useIntervalFn, useWindowFocus } from "@vueuse/core";

export interface AutoRefreshOptions {
  intervalMs: number;
  immediate?: boolean;
}

export default function createAutoRefresh(fetch: () => Promise<void>, { intervalMs, immediate = true }: AutoRefreshOptions) {
  let refCount = 0;
  let watchStop: WatchStopHandle | null = null;
  const interval = ref(intervalMs);

  const { pause, resume, isActive } = useIntervalFn(
    fetch,
    interval,
    { immediate: false } // we control first fetch manually
  );

  const focused = useWindowFocus();

  const start = async () => {
    refCount++;
    if (refCount === 1) {
      if (immediate) {
        await fetch();
      }
      resume();
      watchStop = watch(focused, (isFocused) => (isFocused ? resume() : pause()));
    }
  };

  const stop = () => {
    refCount--;
    if (refCount <= 0) {
      pause();
      watchStop?.();
      watchStop = null;
      refCount = 0;
    }
  };

  const updateInterval = (newIntervalMs: number) => {
    interval.value = newIntervalMs;
  };

  return function useAutoRefresh() {
    onMounted(start);
    onUnmounted(stop);
    return { refreshNow: fetch, isRefreshing: isActive, updateInterval, pause, resume };
  };
}
