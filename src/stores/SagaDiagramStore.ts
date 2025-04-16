import { acceptHMRUpdate, defineStore } from "pinia";
import { ref, watch } from "vue";
import { SagaHistory } from "@/resources/SagaHistory";
import { useFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";

export const useSagaDiagramStore = defineStore("sagaHistory", () => {
  const sagaHistory = ref<SagaHistory | null>(null);
  const sagaId = ref<string | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  // Watch for changes to sagaId and fetch saga history data
  watch(sagaId, async (newSagaId) => {
    if (!newSagaId) {
      sagaHistory.value = null;
      return;
    }

    await fetchSagaHistory(newSagaId);
  });

  function setSagaId(id: string | null) {
    sagaId.value = id;
  }

  async function fetchSagaHistory(id: string) {
    if (!id) return;

    loading.value = true;
    error.value = null;

    try {
      const response = await useFetchFromServiceControl(`sagas/${id}`);

      if (response.status === 404) {
        sagaHistory.value = null;
        error.value = "Saga history not found";
      } else if (!response.ok) {
        sagaHistory.value = null;
        error.value = "Failed to fetch saga history";
      } else {
        const data = await response.json();

        sagaHistory.value = data;
      }
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Unknown error occurred";
      sagaHistory.value = null;
    } finally {
      loading.value = false;
    }
  }

  function clearSagaHistory() {
    sagaHistory.value = null;
    sagaId.value = null;
    error.value = null;
  }

  return {
    sagaHistory,
    sagaId,
    loading,
    error,
    setSagaId,
    fetchSagaHistory,
    clearSagaHistory,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useSagaDiagramStore, import.meta.hot));
}

export type SagaDiagramStore = ReturnType<typeof useSagaDiagramStore>;
