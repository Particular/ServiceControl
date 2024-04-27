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
  () => monitoringHistoryPeriodStore.setHistoryPeriod(route?.query?.historyPeriod?.toString()),
  { immediate: true }
);
</script>

<template>
  <ul class="nav nav-pills period-selector">
    <li
      role="presentation"
      data-bs-placement="top"
      v-for="period in allPeriods"
      :key="period.pVal"
      v-tooltip
      :title="period.refreshIntervalText"
      :class="{ active: period.pVal === selectedPeriod.pVal, notselected: period.pVal !== selectedPeriod.pVal }"
    >
      <a :href="`#`" @click.prevent="selectHistoryPeriod(period)">{{ period.text }}</a>
    </li>
  </ul>
</template>

<style scoped>
.period-selector {
  color: #00a3c4;
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
  color: #00a3c4;
  font-weight: normal;
  background-color: initial;
  border-bottom-color: #00a3c4;
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
