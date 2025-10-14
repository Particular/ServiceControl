import { useCustomChecksStore } from "@/stores/CustomChecksStore";
import { useStoreAutoRefresh } from "./useAutoRefresh";

export default useStoreAutoRefresh("customChecks", useCustomChecksStore, 5000);
