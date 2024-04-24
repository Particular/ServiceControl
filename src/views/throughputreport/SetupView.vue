<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import ThroughputConnectionSettings from "@/resources/ThroughputConnectionSettings";
import { Transport } from "./Transport";
import throughputClient from "@/views/throughputreport/throughputClient";

const testResults = ref<ConnectionTestResults | null>(null);
const settingsInfo = ref<ThroughputConnectionSettings | null>(null);

onMounted(async () => {
  await getTestResults();
});

const transport = computed(() => {
  if (testResults.value == null) {
    return Transport.None;
  }

  return testResults.value.transport as Transport;
});

async function getTestResults() {
  const [test, settings] = await Promise.all([throughputClient.test(), throughputClient.setting()]);
  testResults.value = test;
  settingsInfo.value = settings;
}

function displayTransportNameForInstructions() {
  switch (transport.value) {
    case Transport.AzureStorageQueue:
    case Transport.NetStandardAzureServiceBus:
      return "Azure";
    case Transport.LearningTransport:
      return "Learning Transport";
    case Transport["RabbitMQ.ClassicConventionalRouting"]:
    case Transport["RabbitMQ.ClassicDirectRouting"]:
      return "RabbitMQ";
    case Transport.SQLServer:
      return "Sql Server";
    case Transport.AmazonSQS:
      return "AWS";
  }
}

async function test() {
  await getTestResults();
}
</script>

<template>
  <template v-if="transport === Transport.MSMQ">
    <div class="intro">
      <p>In order for ServicePulse to collect throughput data from MSMQ endpoints, you need to enable metrics for all your endpoints.</p>
      <p>To setup ServicePulse to collect endpoint Metrics <a href="https://docs.particular.net/monitoring/metrics/">read the Metrics documentation</a>.</p>
    </div>
  </template>
  <template v-else>
    <div class="intro">
      <p>In order for ServicePulse to collect throughput data directly from {{ displayTransportNameForInstructions() }} you need to setup the following settings in ServiceControl.</p>
      <p>There are two options to set the settings, you can either set environment variables or alternative is to set it directly in the <code>ServiceControl.exe.config</code> file.</p>
      <p>For more information read <a href="https://docs.particular.net/servicecontrol/creating-config-file">this documentation</a>.</p>
    </div>
    <div class="row">
      <div class="card">
        <div class="card-body">
          <h5 class="card-title">List of settings</h5>
          <ul class="card-text settingsList">
            <li v-for="item in settingsInfo?.broker_settings" :key="item.name">
              <div>
                <strong>{{ item.name }}</strong>
              </div>
              <p>{{ item.description }}</p>
            </li>
          </ul>
        </div>
      </div>
    </div>
    <div class="row"><button class="btn btn-primary actions" type="button" @click="test">Test Connection</button></div>
    <div class="row">
      <h3>Test Connection results</h3>
      <h4>Broker</h4>
      <pre>{{ testResults?.broker_connection_result.diagnostics }}</pre>
      <h4>Audit</h4>
      <pre>{{ testResults?.audit_connection_result.diagnostics }}</pre>
      <h4>Monitoring</h4>
      <pre>{{ testResults?.monitoring_connection_result.diagnostics }}</pre>
    </div>
  </template>
</template>

<style scoped>
.settingsList {
  list-style: none;
  padding-left: 0;
}

.intro {
  margin: 10px 0;
}

.actions {
  margin: 10px 0;
  width: 200px;
}
</style>
