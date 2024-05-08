<script setup lang="ts">
import isRouteSelected from "@/composables/isRouteSelected";
import routeLinks from "@/router/routeLinks";
import { Transport } from "@/views/throughputreport/Transport";
import { computed, onMounted, ref } from "vue";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import throughputClient from "@/views/throughputreport/throughputClient";

const testResults = ref<ConnectionTestResults | null>(null);

onMounted(async () => {
  testResults.value = await throughputClient.test();
});

const transport = computed(() => {
  if (testResults.value == null) {
    return Transport.None;
  }

  return testResults.value.transport as Transport;
});

const transportSupported = computed(() => {
  switch (transport.value) {
    case Transport.None:
    case Transport.MSMQ:
    case Transport.AzureStorageQueue:
    case Transport.LearningTransport:
      return false;
    default:
      return true;
  }
});

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
  <div class="box">
    <div class="row">
      <template v-if="!transportSupported">
        <div class="intro">
          <p>You are using a transport that does not support automatic throughput collection.</p>
          <p>In order for ServicePulse to collect throughput data from your endpoints, you need to ensure that either auditing or metrics are enabled on all your endpoints.</p>
          <p>For more information on auditing and setup instructions <a href="https://docs.particular.net/servicecontrol/audit-instances/">read the Audit documentation</a>.</p>
          <p>For more information on metrics and setup instructions <a href="https://docs.particular.net/monitoring/metrics/">read the Metrics documentation</a>.</p>
        </div>
      </template>
      <template v-else>
        <template v-if="testResults?.broker_connection_result.connection_successful">
          <div>
            <h6><i style="color: green" class="fa fa-check"></i> Successfully connected to {{ transportNameForInstructions() }} for throughput collection.</h6>
          </div>
        </template>
        <template v-else>
          <div>
            <h6><i style="color: red" class="fa fa-times"></i> The connection to {{ transportNameForInstructions() }} was not successfully.</h6>
            <p>You may have not setup all the connection settings, have a look at <RouterLink :to="routeLinks.throughput.setup.setupConnection.link">Connection Setup</RouterLink> tab.</p>
            <p>If you have set the settings but are still having issues, look at the <RouterLink :to="routeLinks.throughput.setup.diagnostics.link">Diagnostics</RouterLink> tab for more information on how to fix the issue.</p>
          </div>
        </template>
      </template>
    </div>
    <br />
    <div class="row">
      <div class="col-sm-12">
        <div class="nav tabs">
          <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughput.setup.setupConnection.link) }">
            <RouterLink :to="routeLinks.throughput.setup.setupConnection.link">Connection Setup</RouterLink>
          </h5>
          <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughput.setup.mask.link) }">
            <RouterLink :to="routeLinks.throughput.setup.mask.link">Mask Report Data</RouterLink>
          </h5>
          <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughput.setup.diagnostics.link) }">
            <RouterLink :to="routeLinks.throughput.setup.diagnostics.link">Diagnostics</RouterLink>
          </h5>
        </div>
      </div>
    </div>
    <div class="intro">
      <RouterView />
    </div>
  </div>
</template>

<style scoped>
.intro {
  margin: 10px 0;
}
</style>
