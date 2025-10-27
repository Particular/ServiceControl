<script setup lang="ts" generic="T">
import { onMounted, onUnmounted, ref, watch } from "vue";
import ItemsPerPage from "@/components/ItemsPerPage.vue";
import PaginationStrip from "@/components/PaginationStrip.vue";
import type DataViewPageModel from "./DataViewPageModel";
import { useServiceControlStore } from "@/stores/ServiceControlStore";

const props = withDefaults(
  defineProps<{
    apiUrl: string;
    itemsPerPageOptions?: number[];
    itemsPerPage?: number;
    autoRefreshSeconds: number;
    showPagination?: boolean;
    showItemsPerPage?: boolean;
  }>(),
  { itemsPerPageOptions: () => [20, 35, 50, 75], itemsPerPage: 50, showPagination: true, showItemsPerPage: false }
);

let refreshTimer: number | undefined;
const viewModel = defineModel<DataViewPageModel<T>>({ required: true });

const store = useServiceControlStore();

const pageNumber = ref(1);
const itemsPerPage = ref(props.itemsPerPage);

watch(
  () => props.autoRefreshSeconds,
  () => {
    stopRefreshTimer();
    startRefreshTimer();
  }
);

watch(itemsPerPage, () => loadData());
watch(pageNumber, () => loadData());

async function loadData() {
  const [response, data] = await store.fetchTypedFromServiceControl<T[]>(`${props.apiUrl}?page=${pageNumber.value}&per_page=${itemsPerPage.value}`);
  if (response.ok) {
    viewModel.value.totalCount = parseInt(response.headers.get("Total-Count") ?? "0");
    viewModel.value.data = data;
  }
}

function startRefreshTimer() {
  if (props.autoRefreshSeconds) {
    refreshTimer = window.setInterval(() => {
      loadData();
    }, props.autoRefreshSeconds * 1000);
  }
}

function stopRefreshTimer() {
  window.clearInterval(refreshTimer);
}

onMounted(() => {
  startRefreshTimer();
  loadData();
});

onUnmounted(() => {
  stopRefreshTimer();
});
</script>

<template>
  <slot name="data"></slot>
  <div class="row">
    <ItemsPerPage v-if="showItemsPerPage" v-model="itemsPerPage" :options="itemsPerPageOptions" />
    <PaginationStrip v-if="showPagination" v-model="pageNumber" :totalCount="viewModel.totalCount" :itemsPerPage="itemsPerPage" />
  </div>
  <slot name="footer"></slot>
</template>
