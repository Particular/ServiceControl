import { useThroughputStore } from "@/stores/ThroughputStore";
import { createStoreAutoRefresh } from "./useAutoRefresh";

export default createStoreAutoRefresh("throughput", useThroughputStore, 60 * 60 * 1000 /* 1 hour */);
