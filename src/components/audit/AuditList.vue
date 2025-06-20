<script setup lang="ts">
import routeLinks from "@/router/routeLinks";
import { FieldNames, useAuditStore } from "@/stores/AuditStore";
import { storeToRefs } from "pinia";
import Message, { MessageStatus } from "@/resources/Message";
import { useRoute, useRouter } from "vue-router";
import ResultsCount from "@/components/ResultsCount.vue";
import { formatDotNetTimespan } from "@/composables/formatUtils";
import FiltersPanel from "@/components/audit/FiltersPanel.vue";
import MessageStatusIcon from "@/components/audit/MessageStatusIcon.vue";
import { onBeforeMount, onUnmounted, ref, watch } from "vue";
import RefreshConfig from "../RefreshConfig.vue";
import useAutoRefresh from "@/composables/autoRefresh";
import throttle from "lodash/throttle";
import LoadingSpinner from "@/components/LoadingSpinner.vue";

const store = useAuditStore();
const { messages, totalCount, sortBy, messageFilterString, selectedEndpointName, itemsPerPage, dateRange } = storeToRefs(store);
const route = useRoute();
const router = useRouter();
const autoRefreshValue = ref<number | null>(null);
const isLoading = ref(false);

const dataRetriever = useAutoRefresh(
  throttle(async () => {
    isLoading.value = true;
    try {
      await store.refresh();
    } finally {
      isLoading.value = false;
    }
  }, 2000),
  null
);

onUnmounted(() => {
  dataRetriever.updateTimeout(null);
});

function navigateToMessage(message: Message) {
  const query = router.currentRoute.value.query;

  router.push({
    path: message.status === MessageStatus.Successful || message.status === MessageStatus.ResolvedSuccessfully ? routeLinks.messages.successMessage.link(message.message_id, message.id) : routeLinks.messages.failedMessage.link(message.id),
    query: { ...query, ...{ back: route.path } },
  });
}

const firstLoad = ref(true);

onBeforeMount(() => {
  setQuery();

  //without setTimeout, this happens before the store is properly initialised, and therefore the query route values aren't applied to the refresh
  setTimeout(async () => {
    await Promise.all([dataRetriever.executeAndResetTimer(), store.loadEndpoints()]);
    firstLoad.value = false;
  }, 0);
});

watch(
  () => router.currentRoute.value.query,
  async () => {
    setQuery();
    await dataRetriever.executeAndResetTimer();
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

  await dataRetriever.executeAndResetTimer();
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

watch(autoRefreshValue, (newValue) => dataRetriever.updateTimeout(newValue));
</script>

<template>
  <div>
    <div class="header">
      <RefreshConfig v-model="autoRefreshValue" :isLoading="isLoading" @manual-refresh="dataRetriever.executeAndResetTimer()" />
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
        <div class="item" @click="navigateToMessage(message)">
          <div class="status">
            <MessageStatusIcon :message="message" />
          </div>
          <div class="message-id">{{ message.message_id }}</div>
          <div class="message-type">{{ message.message_type }}</div>
          <div class="time-sent"><span class="label-name">Time Sent:</span>{{ new Date(message.time_sent).toLocaleString() }}</div>
          <div class="critical-time"><span class="label-name">Critical Time:</span>{{ formatDotNetTimespan(message.critical_time) }}</div>
          <div class="processing-time"><span class="label-name">Processing Time:</span>{{ formatDotNetTimespan(message.processing_time) }}</div>
          <div class="delivery-time"><span class="label-name">Delivery Time:</span>{{ formatDotNetTimespan(message.delivery_time) }}</div>
        </div>
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
.item {
  padding: 0.3rem 0.2rem;
  border: 1px solid #ffffff;
  border-bottom: 1px solid #eee;
  display: grid;
  grid-template-columns: 1.8em 1fr 1fr 1fr 1fr;
  grid-template-rows: 1fr 1fr;
  gap: 0.375rem;
  grid-template-areas:
    "status message-type message-type message-type time-sent"
    "status message-id processing-time critical-time delivery-time";
}
.item:not(:first-child) {
  border-top-color: #eee;
}
.item:hover {
  border-color: #00a3c4;
  background-color: #edf6f7;
  cursor: pointer;
}
.label-name {
  margin-right: 0.25rem;
  color: #777f7f;
}
.status {
  grid-area: status;
}
.message-id {
  grid-area: message-id;
}
.time-sent {
  grid-area: time-sent;
}
.message-type {
  grid-area: message-type;
  font-weight: bold;
  overflow-wrap: break-word;
}
.processing-time {
  grid-area: processing-time;
}
.critical-time {
  grid-area: critical-time;
}
.delivery-time {
  grid-area: delivery-time;
}
</style>
