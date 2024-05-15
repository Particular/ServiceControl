<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import ThroughputConnectionSettings from "@/resources/ThroughputConnectionSettings";
import { Transport } from "@/views/throughputreport/transport";
import throughputClient from "@/views/throughputreport/throughputClient";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import { useIsMonitoringEnabled } from "@/composables/serviceServiceControlUrls";

const testResults = ref<ConnectionTestResults | null>(null);
const settingsInfo = ref<ThroughputConnectionSettings | null>(null);
const transport = computed(() => {
  if (testResults.value == null) {
    return Transport.None;
  }

  return testResults.value.transport as Transport;
});

onMounted(async () => {
  await Promise.all([testConnection(), getSettings()]);
});

async function getSettings() {
  settingsInfo.value = await throughputClient.setting();
}

async function testConnection() {
  testResults.value = await throughputClient.test();
}

function transportNameForInstructions() {
  switch (transport.value) {
    case Transport.AzureStorageQueue:
    case Transport.NetStandardAzureServiceBus:
      return "Azure";
    case Transport.LearningTransport:
      return "Learning Transport";
    case Transport.RabbitMQ:
      return "RabbitMQ";
    case Transport.SQLServer:
      return "Sql Server";
    case Transport.AmazonSQS:
      return "AWS";
  }
}
</script>

<template>
  <div class="row">
    <p>
      In order for ServicePulse to collect throughput data directly from {{ transportNameForInstructions() }} you need to configure the below settings.<br />
      There are two configuration options: as environment variables or directly in the
      <a href="https://docs.particular.net/servicecontrol/creating-config-file"><code>ServiceControl.exe.config</code></a> file. Use `Setting Name` as the environment variable or setting name<br />
    </p>
  </div>
  <div class="row">
    <div class="card">
      <div class="card-body">
        <template v-if="settingsInfo?.broker_settings.length ?? 0 > 0">
          <h5>Broker Settings</h5>
          <p>
            Settings to ensure that throughput data is being collected from {{ transportNameForInstructions() }}.<br />
            Some settings can be automatically configured based on the current transport configuration, so if you have a <i style="color: green" class="fa fa-check"></i> above it means that you don't need to do anything.
          </p>
          <ul class="card-text settingsList">
            <li v-for="item in settingsInfo?.broker_settings" :key="item.name">
              <div><strong>Setting Name:</strong> {{ item.name }}</div>
              <p>
                <em>{{ item.description }}</em>
              </p>
            </li>
          </ul>
        </template>
        <template v-if="useIsMonitoringEnabled()">
          <h5 class="card-title">ServiceControl Settings</h5>
          <p>
            Settings to ensure that throughput data is being collected from the Monitoring instance.<br />
            These settings do not need to be modified unless MSMQ transport is used with the Monitoring instance installed on a different machine to the ServiceControl Error instance.<br />
            For more information read the <a href="TODO">Monitoring</a> and <a href="TODO">ServiceControl</a> settings documentation.
          </p>
          <h7 class="card-title">ServiceControl</h7>
          <ul class="card-text settingsList">
            <li v-for="item in settingsInfo?.service_control_settings" :key="item.name">
              <div><strong>Setting Name:</strong> {{ item.name }}</div>
              <p>
                <em>{{ item.description }}</em>
              </p>
            </li>
          </ul>
          <h7 class="card-title">Monitoring</h7>
          <ul class="card-text settingsList">
            <li v-for="item in settingsInfo?.monitoring_settings" :key="item.name">
              <div><strong>Setting Name:</strong> {{ item.name }}</div>
              <p>
                <em>{{ item.description }}</em>
              </p>
            </li>
          </ul>
        </template>
      </div>
    </div>
  </div>
</template>

<style scoped></style>
