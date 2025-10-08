import createAutoRefresh from "./autoRefresh";
import { useHeartbeatsStore } from "@/stores/HeartbeatsStore";

const store = useHeartbeatsStore();

const useHeartbeatsStoreAutoRefresh = () => ({
  autoRefresh: createAutoRefresh(store.refresh, {
    intervalMs: 5000,
  })(),
  store,
});

export default useHeartbeatsStoreAutoRefresh;
