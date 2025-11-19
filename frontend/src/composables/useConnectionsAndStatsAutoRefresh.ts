import { useConnectionsAndStatsStore } from "@/stores/ConnectionsAndStatsStore";
import { useStoreAutoRefresh } from "./useAutoRefresh";

export default useStoreAutoRefresh("connectionsAndStats", useConnectionsAndStatsStore, 5000);
