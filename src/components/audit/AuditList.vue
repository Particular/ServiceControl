<script setup lang="ts">
import { FieldNames, useAuditStore } from "@/stores/AuditStore";
import { storeToRefs } from "pinia";
import { useRoute, useRouter } from "vue-router";
import ResultsCount from "@/components/ResultsCount.vue";
import FiltersPanel from "@/components/audit/FiltersPanel.vue";
import AuditListItem from "@/components/audit/AuditListItem.vue";
import { onBeforeMount, ref, watch } from "vue";
import RefreshConfig from "../RefreshConfig.vue";
import LoadingSpinner from "@/components/LoadingSpinner.vue";
import createAutoRefresh from "@/composables/autoRefresh";

const store = useAuditStore();
const { messages, totalCount, sortBy, messageFilterString, selectedEndpointName, itemsPerPage, dateRange } = storeToRefs(store);
const route = useRoute();
const router = useRouter();
const autoRefreshValue = ref<number | null>(null);
const { refreshNow, isRefreshing, updateInterval, start, stop } = createAutoRefresh("audit-list", store.refresh, 3000);
const firstLoad = ref(true);

onBeforeMount(() => {
  setQuery();

  //without setTimeout, this happens before the store is properly initialised, and therefore the query route values aren't applied to the refresh
  setTimeout(async () => {
    await Promise.all([refreshNow(), store.loadEndpoints()]);
    firstLoad.value = false;
  }, 0);
});

watch(
  () => router.currentRoute.value.query,
  async () => {
    setQuery();
    await refreshNow();
  },
  { deep: true }
);

const watchHandle = watch([() => route.query, itemsPerPage, sortBy, messageFilterString, selectedEndpointName, dateRange], async () => {
  if (firstLoad.value) {
    return;
  }

  const [fromDate, toDate] = dateRange.value;
  const from = fromDate?.toISOString() ?? "";
  const to = toDate?.toISOString() ?? "";

  await router.push({
    query: {
      sortBy: sortBy.value.property,
      sortDir: sortBy.value.isAscending ? "asc" : "desc",
      filter: messageFilterString.value,
      endpoint: selectedEndpointName.value,
      from,
      to,
      pageSize: itemsPerPage.value,
    },
  });

  await refreshNow();
});

function setQuery() {
  const query = router.currentRoute.value.query;

  watchHandle.pause();

  messageFilterString.value = query.filter ? (query.filter as string) : "";
  sortBy.value =
    query.sortBy && query.sortDir //
      ? { isAscending: query.sortDir === "asc", property: query.sortBy as string }
      : (sortBy.value = { isAscending: false, property: FieldNames.TimeSent });
  itemsPerPage.value = query.pageSize ? parseInt(query.pageSize as string) : 100;
  dateRange.value = query.from && query.to ? [new Date(query.from as string), new Date(query.to as string)] : [];
  selectedEndpointName.value = (query.endpoint ?? "") as string;

  watchHandle.resume();
}

watch(autoRefreshValue, (newValue) => {
  updateInterval(newValue || 0);
  if (newValue === null || newValue === 0) {
    stop();
  } else {
    start();
  }
});
</script>

<template>
  <div>
    <div class="header">
      <RefreshConfig v-model="autoRefreshValue" :isLoading="isRefreshing" @manual-refresh="refreshNow" />
      <div class="row">
        <FiltersPanel />
      </div>
      <div class="row">
        <ResultsCount :displayed="messages.length" :total="totalCount" />
      </div>
    </div>
    <div class="row results-table">
      <LoadingSpinner v-if="firstLoad" />
      <template v-for="message in messages" :key="message.id">
        <AuditListItem :message="message" />
      </template>
    </div>
  </div>
</template>

<style scoped>
@import "../list.css";

.header {
  position: sticky;
  top: -3rem;
  background: #f2f6f7;
  z-index: 100;
  /* set padding/margin so that the sticky version is offset, but not the non-sticky version */
  padding-top: 0.5rem;
  margin-top: -0.5rem;
}

.results-table {
  margin-top: 1rem;
  margin-bottom: 5rem;
  background-color: #ffffff;
}
</style>
