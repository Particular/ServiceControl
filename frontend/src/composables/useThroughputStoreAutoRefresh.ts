import { useThroughputStore } from "@/stores/ThroughputStore";
import { useStoreAutoRefresh } from "./useAutoRefresh";

export default useStoreAutoRefresh("throughput", useThroughputStore, 60 * 60 * 1000 /* 1 hour */);
