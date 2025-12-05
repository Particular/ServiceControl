<script setup lang="ts">
// Composables
import { onMounted, watch, onUnmounted, computed } from "vue";
import { storeToRefs } from "pinia";
import { useMonitoringStore } from "@/stores/MonitoringStore";
// Components
import LicenseNotExpired from "@/components/LicenseNotExpired.vue";
import ServiceControlAvailable from "@/components/ServiceControlAvailable.vue";
import EndpointList from "@/components/monitoring/EndpointList.vue";
import MonitoringNoData from "@/components/monitoring/MonitoringNoData.vue";
import MonitoringHead from "@/components/monitoring/MonitoringHead.vue";
import { useMonitoringHistoryPeriodStore } from "@/stores/MonitoringHistoryPeriodStore";

const monitoringStore = useMonitoringStore();
const monitoringHistoryPeriodStore = useMonitoringHistoryPeriodStore();
const { historyPeriod } = storeToRefs(monitoringHistoryPeriodStore);
const noData = computed(() => monitoringStore.endpointListIsEmpty);

let refreshInterval: number | undefined = undefined;

watch(historyPeriod, async (newValue) => {
  await changeRefreshInterval(newValue.refreshIntervalVal);
});

async function changeRefreshInterval(milliseconds: number) {
  if (refreshInterval) {
    window.clearInterval(refreshInterval);
  }
  await monitoringStore.updateEndpointList();
  refreshInterval = window.setInterval(async () => {
    await monitoringStore.updateEndpointList();
  }, milliseconds);
}

onUnmounted(() => {
  if (refreshInterval) {
    window.clearInterval(refreshInterval);
  }
});

onMounted(async () => {
  await monitoringStore.updateFilterString();
  await changeRefreshInterval(monitoringHistoryPeriodStore.historyPeriod.refreshIntervalVal);
});
</script>

<template>
  <div class="container monitoring-view">
    <ServiceControlAvailable>
      <LicenseNotExpired>
        <MonitoringNoData v-if="noData"></MonitoringNoData>
        <template v-if="!noData">
          <MonitoringHead />
          <EndpointList />
        </template>
      </LicenseNotExpired>
    </ServiceControlAvailable>
  </div>
</template>
