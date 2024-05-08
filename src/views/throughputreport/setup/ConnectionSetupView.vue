<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import ThroughputConnectionSettings from "@/resources/ThroughputConnectionSettings";
import { Transport } from "@/views/throughputreport/Transport";
import throughputClient from "@/views/throughputreport/throughputClient";
import ConnectionTestResults from "@/resources/ConnectionTestResults";

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
      In order for ServicePulse to collect throughput data directly from {{ transportNameForInstructions() }} you need to configure the following settings in ServiceControl.<br />
      There are two options to set the settings, you can either set environment variables or alternative is to set it directly in the <code>ServiceControl.exe.config</code> file.<br />
      For more information read <a href="https://docs.particular.net/servicecontrol/creating-config-file">this documentation</a>.
    </p>
  </div>
  <div class="row">
    <div class="card">
      <div class="card-body">
        <template v-if="settingsInfo?.broker_settings.length ?? 0 > 0">
          <h5>Broker Settings</h5>
          <p>Settings to ensure that throughput data is being collected from {{ transportNameForInstructions() }}.<br /></p>
          <ul class="card-text settingsList">
            <li v-for="item in settingsInfo?.broker_settings" :key="item.name">
              <div>
                <strong>{{ item.name }}</strong>
              </div>
              <p>{{ item.description }}</p>
            </li>
          </ul>
        </template>
        <h5 class="card-title">ServiceControl Settings</h5>
        <p>
          Settings to ensure that throughput data is being collected from Monitoring.<br />
          For more information read <a href="TODO">this documentation</a>.
        </p>
        <ul class="card-text settingsList">
          <li v-for="item in settingsInfo?.service_control_settings" :key="item.name">
            <div>
              <strong>{{ item.name }}</strong>
            </div>
            <p>{{ item.description }}</p>
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>

<style scoped></style>
