import { acceptHMRUpdate, defineStore, storeToRefs } from "pinia";
import { computed, ref, watch } from "vue";
import Configuration from "@/resources/Configuration";
import { useServiceControlStore } from "./ServiceControlStore";

export const useConfigurationStore = defineStore("ConfigurationStore", () => {
  const configuration = ref<Configuration | null>(null);

  const serviceControlStore = useServiceControlStore();
  const { serviceControlUrl } = storeToRefs(serviceControlStore);

  const isMassTransitConnected = computed(() => configuration.value?.mass_transit_connector !== undefined);

  async function refresh() {
    if (!serviceControlUrl.value) return;

    const response = await serviceControlStore.fetchFromServiceControl("configuration");
    configuration.value = await response.json();
  }

  watch(serviceControlUrl, refresh, { immediate: true });

  return {
    configuration,
    refresh,
    isMassTransitConnected,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useConfigurationStore, import.meta.hot));
}

export type ConfigurationStore = ReturnType<typeof useConfigurationStore>;
