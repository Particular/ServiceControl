import createAutoRefresh from "./autoRefresh";
import { useHeartbeatsStore } from "@/stores/HeartbeatsStore";

const useHeartbeatsStoreAutoRefresh = () => {
  const store = useHeartbeatsStore();
  return {
    autoRefresh: createAutoRefresh(store.refresh, {
      intervalMs: 5000,
    })(),
    store,
  };
};

export default useHeartbeatsStoreAutoRefresh;
