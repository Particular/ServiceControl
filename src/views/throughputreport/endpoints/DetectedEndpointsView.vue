<script setup lang="ts">
import DetectedListView from "@/views/throughputreport/endpoints/DetectedListView.vue";
import { DataSource } from "@/views/throughputreport/endpoints/dataSource";
import { UserIndicator } from "@/views/throughputreport/endpoints/userIndicator";
import routeLinks from "@/router/routeLinks";
import { useThroughputStore } from "@/stores/ThroughputStore";
import { storeToRefs } from "pinia";
import FAIcon from "@/components/FAIcon.vue";
import { faTimes } from "@fortawesome/free-solid-svg-icons";

const { isBrokerTransport, hasErrors } = storeToRefs(useThroughputStore());
</script>

<template>
  <template v-if="!isBrokerTransport">
    <template v-if="hasErrors">
      <div class="errorContainer text-center">
        <h6><FAIcon :icon="faTimes" class="text-danger" /> There were some errors collecting usage data.</h6>
        <p>
          You may have not setup all the connection settings, have a look at <RouterLink :to="routeLinks.throughput.setup.connectionSetup.link">Connection Setup in Configuration</RouterLink>.<br />
          If you have set all the connection settings but are still having issues, look at the <RouterLink :to="routeLinks.throughput.setup.diagnostics.link">Diagnostics in Configuration</RouterLink> for more information on how to fix them.
        </p>
      </div>
    </template>
  </template>
  <DetectedListView
    ariaLabel="Detected endpoints"
    :indicator-options="[
      UserIndicator.NServiceBusEndpoint,
      UserIndicator.TransactionalSessionProcessorEndpoint,
      UserIndicator.SendOnlyEndpoint,
      UserIndicator.NServiceBusEndpointNoLongerInUse,
      UserIndicator.PlannedToDecommission,
      UserIndicator.GatewayOrBridgingEndpoint,
      UserIndicator.ServiceControlEndpoint,
    ]"
    :source="DataSource.WellKnownEndpoint"
    column-title="Endpoint Name"
    :show-endpoint-type-placeholder="false"
  >
    <template #nodata> No usage data available yet </template>
  </DetectedListView>
</template>

<style scoped>
.errorContainer {
  margin: 20px;
}
</style>
