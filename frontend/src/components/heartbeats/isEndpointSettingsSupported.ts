import useEnvironmentAndVersionsAutoRefresh from "@/composables/useEnvironmentAndVersionsAutoRefresh";

export const minimumSCVersionForEndpointSettings = "5.9.0";

export default function useIsEndpointSettingsSupported() {
  const { store: environmentStore } = useEnvironmentAndVersionsAutoRefresh();
  return environmentStore.serviceControlIsGreaterThan(minimumSCVersionForEndpointSettings);
}
