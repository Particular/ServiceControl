<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import ThroughputConnectionSettings from "@/resources/ThroughputConnectionSettings";
import { Transport } from "./Transport";
import throughputClient from "@/views/throughputreport/throughputClient";
import ConnectionView from "@/views/throughputreport/setup/ConnectionView.vue";
import MasksView from "@/views/throughputreport/setup/MasksView.vue";

const testResults = ref<ConnectionTestResults | null>(null);
const settingsInfo = ref<ThroughputConnectionSettings | null>(null);
const showConnection = ref<boolean>(false);
const showMasks = ref<boolean>(false);

onMounted(async () => {
  await Promise.all([testConnection(), getSettings()]);

  if (!testResults.value?.broker_connection_result.connection_successful || !testResults.value?.audit_connection_result.connection_successful || !testResults.value?.monitoring_connection_result.connection_successful) {
    showConnectionClicked();
  }
});

const transport = computed(() => {
  if (testResults.value == null) {
    return Transport.None;
  }

  return testResults.value.transport as Transport;
});

async function getSettings() {
  const settings = await throughputClient.setting();
  settingsInfo.value = settings;
}

async function testConnection() {
  const test = await throughputClient.test();
  testResults.value = test;
}

function transportNameForInstructions() {
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

function showConnectionClicked() {
  showConnection.value = true;
  showMasks.value = false;
}

function showMasksClicked() {
  showConnection.value = false;
  showMasks.value = true;
}
</script>

<template>
  <div class="sp-loader" v-if="testResults === null"></div>
  <div v-if="testResults !== null">
    <div class="box">
      <div class="row">
        <div class="intro">
          <p><strong>Connection Setup</strong> - Settings required for collecting throughput data so that a throughput report can be generated</p>
          <p><strong>Mask Report Data</strong> - Hide sensitive information in the throughput report</p>
        </div>
        <template v-if="transport === Transport.MSMQ || transport === Transport.AzureStorageQueue || transport === Transport.LearningTransport">
          <div class="intro">
            <p>You are using a broker that does not support automatic throughput collection.</p>
            <p>In order for ServicePulse to collect throughput data from your endpoints, you need to ensure that either auditing or metrics are enabled on all your endpoints.</p>
            <p>For more information on auditing and setup instructions <a href="https://docs.particular.net/servicecontrol/audit-instances/">read the Audit documentation</a>.</p>
            <p>For more information on metrics and setup instructions <a href="https://docs.particular.net/monitoring/metrics/">read the Metrics documentation</a>.</p>
          </div>
        </template>
        <template v-else>
          <h5>Connection Status</h5>
          <template v-if="testResults?.broker_connection_result?.connection_successful">
            <div>
              <p>Succesfully connected to {{ transportNameForInstructions() }} for throughput collection.</p>
            </div>
          </template>
          <template v-else>
            <div>
              <p>Some errors were encountered while attemping to connect to {{ transportNameForInstructions() }} for throughput collection.</p>
              <p>Please look at the Connection Setup for more details.</p>
            </div>
          </template>
        </template>
      </div>
    </div>
    <div class="row">
      <div class="col-sm-12">
        <div class="nav tabs">
          <div>
            <h5 class="nav-item" :class="{ active: showConnection }">
              <a class="btn btn-secondary actions" role="button" v-on:click="showConnectionClicked()">Connection Setup</a>
            </h5>
            <h5 class="nav-item" :class="{ active: showMasks }">
              <a class="btn btn-secondary actions" role="button" v-on:click="showMasksClicked()">Mask Report Data</a>
            </h5>
          </div>
        </div>
      </div>
    </div>
    <ConnectionView v-if="showConnection" @test="testConnection" :testResults="testResults" :settingsInfo="settingsInfo" :transportDisplayName="transportNameForInstructions()" />
    <MasksView v-if="showMasks" />
  </div>
</template>

<style scoped>
.intro {
  margin: 10px 0;
}

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
