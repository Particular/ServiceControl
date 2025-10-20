import { useEnvironmentAndVersionsStore } from "@/stores/EnvironmentAndVersionsStore";
import { useStoreAutoRefresh } from "./useAutoRefresh";

export default useStoreAutoRefresh("environmentAndVersions", useEnvironmentAndVersionsStore, 5000);
