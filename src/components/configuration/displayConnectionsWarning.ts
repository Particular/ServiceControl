import { computed } from "vue";
import { connectionState, monitoringConnectionState } from "@/composables/serviceServiceControl";
import { useIsMonitoringEnabled } from "@/composables/serviceServiceControlUrls";

export const displayConnectionsWarning = computed(() => {
  return (connectionState.unableToConnect || (monitoringConnectionState.unableToConnect && useIsMonitoringEnabled())) ?? false;
});
