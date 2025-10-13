import { useCustomChecksStore } from "@/stores/CustomChecksStore";
import { createStoreAutoRefresh } from "./useAutoRefresh";

export default createStoreAutoRefresh("customChecks", useCustomChecksStore, 5000);
