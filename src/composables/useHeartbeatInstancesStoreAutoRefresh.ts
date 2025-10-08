import { useHeartbeatInstancesStore } from "@/stores/HeartbeatInstancesStore";
import createAutoRefresh from "./autoRefresh";

const store = useHeartbeatInstancesStore();

const useHeartbeatInstancesStoreAutoRefresh = () => ({
  autoRefresh: createAutoRefresh(store.refresh, {
    intervalMs: 5000,
  })(),
  store,
});

export default useHeartbeatInstancesStoreAutoRefresh;
