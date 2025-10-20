import useEnvironmentAndVersionsAutoRefresh from "@/composables/useEnvironmentAndVersionsAutoRefresh";

export const minimumSCVersionForThroughput = "5.4.0";

export default function useIsThroughputSupported() {
  const { store: environmentStore } = useEnvironmentAndVersionsAutoRefresh();
  return environmentStore.serviceControlIsGreaterThan(minimumSCVersionForThroughput);
}
