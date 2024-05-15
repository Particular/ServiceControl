<script setup lang="ts">
import isRouteSelected from "@/composables/isRouteSelected";
import routeLinks from "@/router/routeLinks";
import { isBrokerTransport, transportNameForInstructions } from "@/views/throughputreport/transport";
import { onMounted, ref } from "vue";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import throughputClient from "@/views/throughputreport/throughputClient";

const testResults = ref<ConnectionTestResults | null>(null);

onMounted(async () => {
  testResults.value = await throughputClient.test();
});
</script>

<template>
  <div class="box">
    <div class="row">
      <template v-if="!isBrokerTransport">
        <div class="intro">
          <p>You are using a transport that does not support automatic throughput collection.</p>
          <p>
            In order for ServicePulse to collect throughput data from your endpoints, you need to ensure that either Audit or Monitoring (metrics) are enabled on all your endpoints.<br />
            Read the <a href="https://docs.particular.net/servicecontrol/audit-instances/">Audit documentation</a> and the <a href="https://docs.particular.net/monitoring/metrics/">Monitoring documentation</a> for setup instructions .
          </p>
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
            <p>
              You may have not setup all the connection settings, have a look at the <RouterLink :to="routeLinks.throughput.setup.connectionSetup.link">Connection Setup</RouterLink> tab.<br />
              If you have set the settings but are still having issues, look at the <RouterLink :to="routeLinks.throughput.setup.diagnostics.link">Diagnostics</RouterLink> tab for more information on how to fix the issue.
            </p>
          </div>
        </template>
        <div class="alert alert-info">
          Note: enabling <a href="https://docs.particular.net/servicecontrol/audit-instances/">Audit</a> or <a href="https://docs.particular.net/monitoring/metrics/">Monitoring</a> is not mandatory when throughput is being collected from the broker,
          but doing so highly improves the accuracy of the throughput data and Endpoint identification.
        </div>
      </template>
    </div>
    <br v-if="isBrokerTransport" />
    <div class="row">
      <div class="col-sm-12">
        <div class="nav tabs">
          <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughput.setup.connectionSetup.link) }">
            <RouterLink :to="routeLinks.throughput.setup.connectionSetup.link">Connection Setup</RouterLink>
          </h5>
          <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughput.setup.diagnostics.link) }">
            <RouterLink :to="routeLinks.throughput.setup.diagnostics.link">Diagnostics</RouterLink>
          </h5>
          <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughput.setup.mask.link) }">
            <RouterLink :to="routeLinks.throughput.setup.mask.link">Mask Report Data</RouterLink>
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
