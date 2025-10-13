import { useHeartbeatInstancesStore } from "@/stores/HeartbeatInstancesStore";
import { createStoreAutoRefresh } from "./useAutoRefresh";

export default createStoreAutoRefresh("heartbeatInstances", useHeartbeatInstancesStore, 5000);
