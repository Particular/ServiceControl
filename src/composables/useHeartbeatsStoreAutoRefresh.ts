import { useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { createStoreAutoRefresh } from "./useAutoRefresh";

export default createStoreAutoRefresh("heartbeats", useHeartbeatsStore, 5000);
