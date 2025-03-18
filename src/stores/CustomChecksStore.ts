import { useDeleteFromServiceControl, useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import CustomCheck from "@/resources/CustomCheck";
import { acceptHMRUpdate, defineStore } from "pinia";
import { ref, watch } from "vue";
import useAutoRefresh from "@/composables/autoRefresh";

export const useCustomChecksStore = defineStore("CustomChecksStore", () => {
  const prefix = "customchecks/";

  const pageNumber = ref(1);
  const failingCount = ref(0);
  const failedChecks = ref<CustomCheck[]>([]);

  const dataRetriever = useAutoRefresh(async () => {
    try {
      const [response, data] = await useTypedFetchFromServiceControl<CustomCheck[]>(`customchecks?status=fail&page=${pageNumber.value}`);
      failedChecks.value = data;
      failingCount.value = parseInt(response.headers.get("Total-Count") ?? "0");
    } catch (e) {
      failedChecks.value = [];
      failingCount.value = 0;
      throw e;
    }
  }, 5000);

  watch(pageNumber, () => dataRetriever.executeAndResetTimer());

  async function dismissCustomCheck(id: string) {
    await dataRetriever.executeAndResetTimer(async () => {
      // NOTE: If it takes more than the refresh interval for ServiceControl to delete the check it will reappear
      failedChecks.value = failedChecks.value.filter((x) => x.id !== id);
      failingCount.value--;

      // HINT: This is required to handle the difference between ServiceControl 4 and 5
      const guid = id.toLocaleLowerCase().startsWith(prefix) ? id.substring(prefix.length) : id;
      await useDeleteFromServiceControl(`${prefix}${guid}`);
    });
  }

  return {
    dismissCustomCheck,
    pageNumber,
    failingCount,
    failedChecks,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useCustomChecksStore, import.meta.hot));
}

export type CustomChecksStore = ReturnType<typeof useCustomChecksStore>;
