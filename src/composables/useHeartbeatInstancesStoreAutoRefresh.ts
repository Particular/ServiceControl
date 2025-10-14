import { useHeartbeatInstancesStore } from "@/stores/HeartbeatInstancesStore";
import { useStoreAutoRefresh } from "./useAutoRefresh";

export default useStoreAutoRefresh("heartbeatInstances", useHeartbeatInstancesStore, 5000);
