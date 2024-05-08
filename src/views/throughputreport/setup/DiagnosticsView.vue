<script setup lang="ts">
import { onMounted, ref } from "vue";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import throughputClient from "@/views/throughputreport/throughputClient";

const testResults = ref<ConnectionTestResults | null>(null);
const loading = ref(true);

onMounted(async () => {
  await testConnection();
});

async function testConnection() {
  loading.value = true;
  testResults.value = await throughputClient.test();
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
      <h5>Broker</h5>
      <pre>{{ testResults?.broker_connection_result.diagnostics }}</pre>
      <h5>Audit</h5>
      <pre>{{ testResults?.audit_connection_result.diagnostics }}</pre>
      <h5>Monitoring</h5>
      <pre>{{ testResults?.monitoring_connection_result.diagnostics }}</pre>
    </template>
  </div>
  <div class="row">
    <div class="col-6">
      <button class="btn btn-primary actions" type="button" :disabled="loading" @click="testConnection()">Refresh Connection Test</button>
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
