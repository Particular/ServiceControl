<script setup lang="ts">
import routeLinks from "@/router/routeLinks";
import isRouteSelected from "@/composables/isRouteSelected";
import { UserIndicator } from "@/views/throughputreport/endpoints/userIndicator";
import { userIndicatorMapper } from "@/views/throughputreport/endpoints/userIndicatorMapper";
import { ref } from "vue";
import { isBrokerTransport } from "@/views/throughputreport/transport";

const showLegend = ref(true);
const legendOptions = new Map<UserIndicator, string>([
  [UserIndicator.NServiceBusEndpoint, "Known NServiceBus Endpoint"],
  [UserIndicator.NServiceBusEndpointNoLongerInUse, "NServiceBus Endpoint that is no longer in use, usually this would have zero throughput"],
  [UserIndicator.SendOnlyOrTransactionSessionEndpoint, "If the endpoint has no throughput or the endpoint has Transactional Session feature enabled"],
  [UserIndicator.PlannedToDecommission, "If the endpoint is planned to no longer be used in the next 30 days"],
  [UserIndicator.NotNServiceBusEndpoint, "Not a NServiceBus Endpoint"],
]);

function showHideOptionsLegend() {
  showLegend.value = !showLegend.value;
}
</script>

<template>
  <div class="box">
    <div class="row">
      <p>
        Set an Endpoint Type for all detected endpoints and detected broker queues with the most appropriate option.<br />
        Use the filters to bulk set the Endpoint Types on similar named endpoints/queues.<br />
        <a href="#" @click.prevent="showHideOptionsLegend()">{{ showLegend ? "Hide" : "Show" }} options meaning.</a>
      </p>
      <div v-show="showLegend" class="alert alert-info">
        <div v-for="[key, value] in legendOptions" :key="key">
          <strong>{{ userIndicatorMapper.get(key) }}</strong> - {{ value }}.
        </div>
      </div>
    </div>
    <div class="row">
      <div class="col-sm-12">
        <div class="nav tabs">
          <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughput.endpoints.detectedEndpoints.link) }">
            <RouterLink :to="routeLinks.throughput.endpoints.detectedEndpoints.link">Detected Endpoints</RouterLink>
          </h5>
          <h5 v-if="isBrokerTransport" class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughput.endpoints.detectedBrokerQueues.link) }">
            <RouterLink :to="routeLinks.throughput.endpoints.detectedBrokerQueues.link">Detected Broker Queues</RouterLink>
          </h5>
        </div>
      </div>
    </div>
    <div class="intro">
      <RouterView />
    </div>
  </div>
</template>

<style scoped></style>
