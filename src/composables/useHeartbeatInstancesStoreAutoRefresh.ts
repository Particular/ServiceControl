import { useHeartbeatInstancesStore } from "@/stores/HeartbeatInstancesStore";
import createAutoRefresh from "./autoRefresh";

const useHeartbeatInstancesStoreAutoRefresh = () => {
  const store = useHeartbeatInstancesStore();
  return {
    autoRefresh: createAutoRefresh(store.refresh, {
      intervalMs: 5000,
    })(),
    store,
  };
};

export default useHeartbeatInstancesStoreAutoRefresh;
