<script setup lang="ts">
// Composables
import { onMounted, watch, onUnmounted, computed } from "vue";
import { storeToRefs } from "pinia";
import { licenseStatus } from "@/composables/serviceLicense";
import { useMonitoringStore } from "@/stores/MonitoringStore";
// Components
import LicenseExpired from "@/components/LicenseExpired.vue";
import ServiceControlNotAvailable from "@/components/ServiceControlNotAvailable.vue";
import EndpointList from "@/components/monitoring/EndpointList.vue";
import MonitoringNoData from "@/components/monitoring/MonitoringNoData.vue";
import MonitoringHead from "@/components/monitoring/MonitoringHead.vue";
import { useMonitoringHistoryPeriodStore } from "@/stores/MonitoringHistoryPeriodStore";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";

const monitoringStore = useMonitoringStore();
const monitoringHistoryPeriodStore = useMonitoringHistoryPeriodStore();
const { historyPeriod } = storeToRefs(monitoringHistoryPeriodStore);
const noData = computed(() => monitoringStore.endpointListIsEmpty);
const { store: connectionStore } = useConnectionsAndStatsAutoRefresh();
const connectionState = connectionStore.connectionState;

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
  <LicenseExpired />
  <template v-if="!licenseStatus.isExpired">
    <div class="container monitoring-view">
      <ServiceControlNotAvailable />
      <template v-if="connectionState.connected">
        <MonitoringNoData v-if="noData"></MonitoringNoData>
        <template v-if="!noData">
          <MonitoringHead />
          <EndpointList />
        </template>
      </template>
    </div>
  </template>
</template>
