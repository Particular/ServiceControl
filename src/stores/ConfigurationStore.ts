import { acceptHMRUpdate, defineStore } from "pinia";
import { ref } from "vue";
import Configuration from "@/resources/Configuration";
import { useFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";

export const useConfigurationStore = defineStore("ConfigurationStore", () => {
  const configuration = ref<Configuration | null>(null);

  async function loadConfig() {
    const response = await useFetchFromServiceControl("configuration");
    configuration.value = await response.json();
  }

  return {
    configuration,
    loadConfig,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useConfigurationStore, import.meta.hot));
}

export type ConfigurationStore = ReturnType<typeof useConfigurationStore>;
