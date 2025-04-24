<script setup lang="ts">
import FilterInput from "../FilterInput.vue";
import GroupBy from "./MonitoringGroupBy.vue";
import PeriodSelector from "./MonitoringHistoryPeriod.vue";
import { useMonitoringStore } from "@/stores/MonitoringStore";

const monitoringStore = useMonitoringStore();
</script>

<template>
  <div class="filter-group filter-monitoring">
    <PeriodSelector />
    <GroupBy />
    <FilterInput v-model="monitoringStore.filterString" />
  </div>
</template>

<style scoped>
.filter-group.filter-monitoring {
  width: 100%;
  margin-top: 0.5em;
}

.filter-group {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  width: 50%;
  position: relative;
  top: -3px;
  margin-top: -26px;
}

.filter-group > *:not(:first-child) {
  margin-left: 1.5em;
}

.filter-input input {
  display: inline-block;
  width: 100%;
  padding-right: 10px;
  padding-left: 30px;
  border: 1px solid #aaa;
  border-radius: 4px;
}

div.filter-input {
  position: relative;
  width: 280px;
}

.filter-input:before {
  font-family: "FontAwesome";
  width: 1.43em;
  content: "\f0b0";
  color: #919e9e;
  position: absolute;
  top: calc(50% - 0.7em);
  left: 0.75em;
}
</style>
