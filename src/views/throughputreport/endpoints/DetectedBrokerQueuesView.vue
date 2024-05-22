<script setup lang="ts">
import DetectedListView from "@/views/throughputreport/endpoints/DetectedListView.vue";
import { DataSource } from "@/views/throughputreport/endpoints/dataSource";
import { UserIndicator } from "@/views/throughputreport/endpoints/userIndicator";
import { onMounted, ref } from "vue";
import throughputClient from "@/views/throughputreport/throughputClient";
import routeLinks from "@/router/routeLinks";
import ReportGenerationState from "@/resources/ReportGenerationState";
import { useThroughputStore } from "@/stores/ThroughputStore";
import { storeToRefs } from "pinia";

const reportAvailable = ref<ReportGenerationState | null>(null);

const store = useThroughputStore();
const { testResults } = storeToRefs(store);

onMounted(async () => {
  reportAvailable.value = await throughputClient.reportAvailable();
});
</script>

<template>
  <template v-if="!testResults?.broker_connection_result.connection_successful">
    <div class="errorContainer text-center">
      <h6><i style="color: red" class="fa fa-times"></i> The connection to {{ store.transportNameForInstructions() }} was not successful.</h6>
      <p>
        You may have not setup all the connection settings, have a look at <RouterLink :to="routeLinks.throughput.setup.connectionSetup.link">Connection Setup in Configuration</RouterLink>.<br />
        If you have set the settings but are still having issues, look at the <RouterLink :to="routeLinks.throughput.setup.diagnostics.link">Diagnostics in Configuration</RouterLink> for more information on how to fix the issue.
      </p>
    </div>
  </template>
  <DetectedListView
    :indicator-options="[UserIndicator.NServiceBusEndpoint, UserIndicator.NotNServiceBusEndpoint, UserIndicator.SendOnlyOrTransactionSessionEndpoint, UserIndicator.NServiceBusEndpointNoLongerInUse, UserIndicator.PlannedToDecommission]"
    :source="DataSource.broker"
    column-title="Queue Name"
  >
    <template #nodata> No usage data available yet </template>
  </DetectedListView>
</template>

<style scoped>
.errorContainer {
  margin: 20px;
}
</style>
