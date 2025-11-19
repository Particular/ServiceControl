import { useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { useStoreAutoRefresh } from "./useAutoRefresh";

export default useStoreAutoRefresh("heartbeats", useHeartbeatsStore, 5000);
