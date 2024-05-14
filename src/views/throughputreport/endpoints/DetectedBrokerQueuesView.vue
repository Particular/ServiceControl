<script setup lang="ts">
import DetectedListView from "@/views/throughputreport/endpoints/DetectedListView.vue";
import { DataSource } from "@/views/throughputreport/endpoints/dataSource";
import { UserIndicator } from "@/views/throughputreport/endpoints/userIndicator";
import { onMounted, ref } from "vue";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import throughputClient from "@/views/throughputreport/throughputClient";
import routeLinks from "@/router/routeLinks";
import { transportNameForInstructions } from "../transport";
import ReportGenerationState from "@/resources/ReportGenerationState";

const testResults = ref<ConnectionTestResults | null>(null);
const reportAvailable = ref<ReportGenerationState | null>(null);

onMounted(async () => {
  const [test, report] = await Promise.all([throughputClient.test(), throughputClient.reportAvailable()]);
  testResults.value = test;
  reportAvailable.value = report;
});
</script>

<template>
  <template v-if="!testResults?.broker_connection_result.connection_successful">
    <div class="errorContainer text-center">
      <h6><i style="color: red" class="fa fa-times"></i> The connection to {{ transportNameForInstructions() }} was not successful.</h6>
      <p>
        You may have not setup all the connection settings, have a look at <RouterLink :to="routeLinks.throughput.setup.connectionSetup.link">Connection Setup in Configuration</RouterLink>.<br />
        If you have set the settings but are still having issues, look at the <RouterLink :to="routeLinks.throughput.setup.diagnostics.link">Diagnostics in Configuration</RouterLink> for more information on how to fix the issue.
      </p>
    </div>
  </template>
  <DetectedListView
    v-if="testResults?.broker_connection_result.connection_successful || reportAvailable?.report_can_be_generated"
    :indicator-options="[UserIndicator.NServiceBusEndpoint, UserIndicator.NotNServiceBusEndpoint, UserIndicator.SendOnlyOrTransactionSessionEndpoint, UserIndicator.NServiceBusEndpointNoLongerInUse, UserIndicator.PlannedToDecommission]"
    :source="DataSource.broker"
    column-title="Queue Name"
  >
    <template #nodata> No throughput data available yet </template>
  </DetectedListView>
</template>

<style scoped>
.errorContainer {
  margin: 20px;
}
</style>
