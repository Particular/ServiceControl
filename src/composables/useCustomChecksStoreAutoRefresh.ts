import { useCustomChecksStore } from "@/stores/CustomChecksStore";
import createAutoRefresh from "./autoRefresh";

const store = useCustomChecksStore();

const useCustomChecksStoreAutoRefresh = () => ({
  autoRefresh: createAutoRefresh(store.refresh, {
    intervalMs: 5000,
  })(),
  store,
});

export default useCustomChecksStoreAutoRefresh;
