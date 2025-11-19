<script setup lang="ts">
import { storeToRefs } from "pinia";
import { useMonitoringHistoryPeriodStore, type MonitoringHistoryPeriod } from "@/stores/MonitoringHistoryPeriodStore";
import { useRoute } from "vue-router";
import { watch } from "vue";

const monitoringHistoryPeriodStore = useMonitoringHistoryPeriodStore();
const allPeriods = monitoringHistoryPeriodStore.allPeriods;
const { historyPeriod: selectedPeriod } = storeToRefs(monitoringHistoryPeriodStore);

async function selectHistoryPeriod(period: MonitoringHistoryPeriod) {
  await monitoringHistoryPeriodStore.setHistoryPeriod(period.pVal.toString());
}

const route = useRoute();
watch(
  () => route.query.historyPeriod,
  async () => {
    await monitoringHistoryPeriodStore.setHistoryPeriod(route?.query?.historyPeriod?.toString());
  },
  { immediate: true }
);
</script>

<template>
  <ul aria-label="history-period-list" class="nav nav-pills period-selector">
    <li
      data-bs-placement="top"
      v-for="period in allPeriods"
      :key="period.pVal"
      :aria-label="period.pVal.toString()"
      v-tippy="period.refreshIntervalText"
      :class="{ active: period.pVal === selectedPeriod.pVal, notselected: period.pVal !== selectedPeriod.pVal }"
      :aria-selected="period.pVal === selectedPeriod.pVal"
    >
      <a :href="`#`" @click.prevent="selectHistoryPeriod(period)">{{ period.text }}</a>
    </li>
  </ul>
</template>

<style scoped>
.period-selector {
  color: var(--sp-blue);
}

.nav li {
  display: flex;
}

.nav-pills.period-selector > li > a {
  border-radius: 0px;
  border-bottom: 3px solid transparent;
  padding: 10px 6px;
}

.nav-pills.period-selector > li > a:hover {
  color: var(--sp-blue);
  font-weight: normal;
  background-color: initial;
  border-bottom-color: var(--sp-blue);
}

.nav.period-selector > li > a {
  padding: 10px 6px;
}

.nav-pills.period-selector > li.active > a,
.nav-pills.period-selector > li.active > a:hover,
.nav-pills.period-selector > li.active > a:focus {
  color: #000;
  font-weight: bold;
  background-color: initial;
  border-bottom-color: #000;
}

.nav-pills.period-selector > li > a:hover {
  text-decoration: none;
}

.nav-pills > li + li {
  margin-left: 2px;
}
</style>
