<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import ThroughputConnectionSettings from "@/resources/ThroughputConnectionSettings";
import { Transport } from "@/views/throughputreport/transport";
import throughputClient from "@/views/throughputreport/throughputClient";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import { useIsMonitoringEnabled } from "@/composables/serviceServiceControlUrls";
import ConfigurationCode from "@/views/throughputreport/setup/ConfigurationCode.vue";

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
      In order for ServicePulse to collect usage data directly from {{ transportNameForInstructions() }} you need to configure the below settings.<br />
      There are two configuration options, as environment variables or directly in the
      <a href="https://docs.particular.net/servicecontrol/creating-config-file"><code>ServiceControl.exe.config</code></a> file.
    </p>
  </div>
  <template v-if="settingsInfo?.broker_settings.length ?? 0 > 0">
    <div class="row configuration">
      <div class="col-12">
        <h4>Broker Settings</h4>
        <p class="nogap">
          Settings to ensure that usage data is being collected from {{ transportNameForInstructions() }}.<br />
          Some settings can be automatically configured based on the current transport configuration, so if you have a <i style="color: green" class="fa fa-check"></i> above it means that ServiceControl has successfully connected to
          {{ transportNameForInstructions() }}.
        </p>
        <ConfigurationCode :settings="settingsInfo?.broker_settings ?? []">
          <template #configInstructions>
            <div>Paste the settings above in the <code>ServiceControl.exe.config</code> file of the ServiceControl Error instance.</div>
          </template>
          <template #environmentVariableInstructions>
            <div>Execute the above instructions in a terminal to set the environment variables, these variables need to be set for the user that in running the ServiceControl Error instance.</div>
          </template>
        </ConfigurationCode>
      </div>
    </div>
  </template>
  <template v-if="useIsMonitoringEnabled()">
    <div class="row configuration">
      <div class="col-12">
        <h4>ServiceControl Settings</h4>
        <p class="nogap">
          Settings to ensure that usage data is being collected from the Monitoring instance.<br />
          For more information read the <a href="https://docs.particular.net/servicecontrol/creating-config-file">ServiceControl</a> settings documentation.
        </p>
        <ConfigurationCode :settings="settingsInfo?.service_control_settings ?? []">
          <template #configInstructions>
            <div>Paste the settings above in the <code>ServiceControl.exe.config</code> file of the ServiceControl Error instance.</div>
          </template>
          <template #environmentVariableInstructions>
            <div>Execute the above instructions in a terminal to set the environment variables, these variables need to be set for the user that in running the ServiceControl Error instance.</div>
          </template>
        </ConfigurationCode>
      </div>
    </div>
    <div class="row configuration">
      <div class="col-12">
        <h4>Monitoring Settings</h4>
        <p class="nogap">
          Settings to ensure that usage data is being collected from the Monitoring instance.<br />
          These settings do not need to be modified unless MSMQ transport is used with the Monitoring instance installed on a different machine to the ServiceControl Error instance.<br />
          For more information read the <a href="https://docs.particular.net/servicecontrol/monitoring-instances/installation/creating-config-file">Monitoring</a> settings documentation.
        </p>
        <ConfigurationCode :settings="settingsInfo?.monitoring_settings ?? []" configFileName="ServiceControl.Monitoring.exe.config">
          <template #configInstructions>
            <div>Paste the settings above in the <code>ServiceControl.Monitoring.exe.config</code> file of the ServiceControl Monitoring instance.</div>
          </template>
          <template #environmentVariableInstructions>
            <div>Execute the above instructions in a terminal to set the environment variables, these variables need to be set for the user that in running the ServiceControl Monitoring instance.</div>
          </template>
        </ConfigurationCode>
      </div>
    </div>
  </template>
</template>

<style scoped>
.configuration {
  margin-bottom: 15px;
}
.nogap {
  margin-bottom: 0;
}
</style>
