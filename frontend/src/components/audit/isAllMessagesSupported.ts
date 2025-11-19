import useEnvironmentAndVersionsAutoRefresh from "@/composables/useEnvironmentAndVersionsAutoRefresh";

export const minimumSCVersionForAllMessages = "6.6.0";

export default function useIsAllMessagesSupported() {
  const { store: environmentStore } = useEnvironmentAndVersionsAutoRefresh();
  return environmentStore.serviceControlIsGreaterThan(minimumSCVersionForAllMessages);
}
