import { useCustomChecksStore } from "@/stores/CustomChecksStore";
import createAutoRefresh from "./autoRefresh";

const useCustomChecksStoreAutoRefresh = () => {
  const store = useCustomChecksStore();
  return {
    autoRefresh: createAutoRefresh(store.refresh, {
      intervalMs: 5000,
    })(),
    store,
  };
};

export default useCustomChecksStoreAutoRefresh;
