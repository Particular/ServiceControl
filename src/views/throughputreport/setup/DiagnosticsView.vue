<script setup lang="ts">
import ConnectionResultView from "@/views/throughputreport/setup/ConnectionResultView.vue";
import { useIsMonitoringEnabled } from "@/composables/serviceServiceControlUrls";
import { storeToRefs } from "pinia";
import { ref } from "vue";
import useThroughputStoreAutoRefresh from "@/composables/useThroughputStoreAutoRefresh";

const { store } = useThroughputStoreAutoRefresh();
const { testResults, isBrokerTransport } = storeToRefs(store);
const loading = ref(false);

async function testConnection() {
  loading.value = true;
  await store.refresh();
  loading.value = false;
}
</script>

<template>
  <div class="row">
    <h6>Test Connection Results</h6>
    <template v-if="loading">
      <div class="sp-loader" />
    </template>
    <template v-else>
      <ConnectionResultView v-if="isBrokerTransport && testResults !== null" title="Broker" :result="testResults.broker_connection_result" />
      <ConnectionResultView v-if="testResults !== null" title="Audit" :result="testResults.audit_connection_result">
        <template #instructions>
          <a href="https://docs.particular.net/servicecontrol/servicecontrol-instances/remotes#configuration" target="_blank">Learn how to configure audit instances</a>
        </template>
      </ConnectionResultView>
      <ConnectionResultView v-if="useIsMonitoringEnabled() && testResults !== null" title="Monitoring" :result="testResults.monitoring_connection_result">
        <template #instructions>
          <a href="https://docs.particular.net/servicecontrol/monitoring-instances/installation/creating-config-file" target="_blank">Learn how to configure monitor instances</a>
        </template>
      </ConnectionResultView>
    </template>
  </div>
  <div class="row">
    <div class="col-6">
      <button class="btn btn-primary actions" type="button" :disabled="loading" @click="testConnection()" aria-label="Refresh Connection Test">Refresh Connection Test</button>
    </div>
  </div>
</template>

<style scoped>
.sp-loader {
  width: 100%;
  height: 90vh;
  margin-top: -100px;
  background-image: url("@/assets/sp-loader.gif");
  background-size: 150px 150px;
  background-position: center center;
  background-repeat: no-repeat;
}
</style>
