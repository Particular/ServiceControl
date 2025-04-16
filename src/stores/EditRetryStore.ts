import { acceptHMRUpdate, defineStore } from "pinia";
import { ref } from "vue";
import { EditAndRetryConfig } from "@/resources/Configuration";
import { useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";

export const useEditRetryStore = defineStore("EditRetryStore", () => {
  const config = ref<EditAndRetryConfig>({ enabled: false, locked_headers: [], sensitive_headers: [] });

  async function loadConfig() {
    const [, data] = await useTypedFetchFromServiceControl<EditAndRetryConfig>("edit/config");
    config.value = data;
  }

  return {
    config,
    loadConfig,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useEditRetryStore, import.meta.hot));
}

export type EditRetryStore = ReturnType<typeof useEditRetryStore>;
