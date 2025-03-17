import { computed } from "vue";
import { useConfiguration } from "./configuration";

export function useIsMassTransitConnected() {
  const configuration = useConfiguration();
  return computed(() => configuration.value?.mass_transit_connector !== undefined);
}
