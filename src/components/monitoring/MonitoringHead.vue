<script setup lang="ts">
import MonitoringFilter from "./MonitoringFilter.vue";
import { useConfigurationStore } from "@/stores/ConfigurationStore";
import { storeToRefs } from "pinia";

const configurationStore = useConfigurationStore();
const { isMassTransitConnected } = storeToRefs(configurationStore);
</script>

<template>
  <div class="row monitoring-head">
    <div class="col-sm-4 no-side-padding list-section">
      <h1>Endpoints overview</h1>
    </div>
    <div class="col-sm-8 no-side-padding toolbar-menus">
      <MonitoringFilter />
    </div>
    <div class="col-12" v-if="isMassTransitConnected">
      <div class="alert alert-info">MassTransit endpoints are currently not supported by monitoring functionality and will not show in this view.</div>
    </div>
  </div>
</template>

<style scoped>
.monitoring-head h1 {
  margin-bottom: 10px;
  text-overflow: ellipsis;
  overflow: hidden;
  white-space: nowrap;
}
</style>
