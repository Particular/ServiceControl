import { onMounted, onUnmounted } from "vue";
import createAutoRefresh from "./autoRefresh";

export function useAutoRefresh(name: string, refresh: () => Promise<void>, intervalMs: number) {
  const { start, stop } = createAutoRefresh(name, refresh, intervalMs);

  function useAutoRefresh() {
    onMounted(start);
    onUnmounted(stop);
  }

  return useAutoRefresh;
}

/**
 * Creates a singleton auto-refresh composable for a Pinia store.
 * This handles the timing issue where the store needs to be called within a component lifecycle
 * but the auto-refresh manager needs to be a singleton.
 *
 * @param name - Name for logging purposes
 * @param useStore - Function that returns the Pinia store (called within component lifecycle)
 * @param intervalMs - Refresh interval in milliseconds
 * @returns A composable function that sets up auto-refresh and returns the store
 */
export function createStoreAutoRefresh<TStore extends { refresh: () => Promise<void> }>(name: string, useStore: () => TStore, intervalMs: number) {
  const refresh = () => {
    if (!store) {
      return Promise.resolve();
    }
    return store.refresh();
  };
  let store: TStore | null = null;
  const autoRefresh = useAutoRefresh(name, refresh, intervalMs);

  return () => {
    store = useStore();
    autoRefresh();
    return { store };
  };
}
