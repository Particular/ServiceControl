import { useThroughputStore } from "@/stores/ThroughputStore";
import createAutoRefresh from "./autoRefresh";

const useThroughputStoreAutoRefresh = () => {
  const store = useThroughputStore();
  return {
    autoRefresh: createAutoRefresh(store.refresh, {
      intervalMs: 60 * 60 * 1000 /* 1 hour */,
    })(),
    store,
  };
};

export default useThroughputStoreAutoRefresh;
