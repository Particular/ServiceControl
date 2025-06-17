<script setup lang="ts">
import DetectedListView from "@/views/throughputreport/endpoints/DetectedListView.vue";
import { DataSource } from "@/views/throughputreport/endpoints/dataSource";
import { UserIndicator } from "@/views/throughputreport/endpoints/userIndicator";
import routeLinks from "@/router/routeLinks";
import { useThroughputStore } from "@/stores/ThroughputStore";
import { storeToRefs } from "pinia";

const store = useThroughputStore();
const { testResults } = storeToRefs(store);
</script>

<template>
  <template v-if="!testResults?.broker_connection_result.connection_successful">
    <div class="errorContainer text-center">
      <h6><i style="color: red" class="fa fa-times"></i> The connection to {{ store.transportNameForInstructions() }} was not successful.</h6>
      <p>
        You may have not setup all the connection settings, have a look at <RouterLink :to="routeLinks.throughput.setup.connectionSetup.link">Connection Setup in Configuration</RouterLink>.<br />
        If you have set all the connection settings but are still having issues, look at the <RouterLink :to="routeLinks.throughput.setup.diagnostics.link">Diagnostics in Configuration</RouterLink> for more information on how to fix them.
      </p>
    </div>
  </template>
  <DetectedListView
    ariaLabel="Detected broker queues"
    :indicator-options="[
      UserIndicator.NServiceBusEndpoint,
      UserIndicator.NotNServiceBusEndpoint,
      UserIndicator.TransactionalSessionProcessorEndpoint,
      UserIndicator.SendOnlyEndpoint,
      UserIndicator.NServiceBusEndpointNoLongerInUse,
      UserIndicator.PlannedToDecommission,
    ]"
    :source="DataSource.Broker"
    column-title="Queue Name"
    :show-endpoint-type-placeholder="true"
  >
    <template #nodata> No usage data available yet </template>
  </DetectedListView>
</template>

<style scoped>
.errorContainer {
  margin: 20px;
}
</style>
