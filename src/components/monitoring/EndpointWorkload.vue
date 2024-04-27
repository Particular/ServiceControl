<script setup lang="ts">
import { type ExtendedEndpointDetails } from "@/resources/MonitoringEndpoint";
import { formatGraphDecimalFromNumber, largeGraphsMinimumYAxis } from "./formatGraph";
import LargeGraph from "./LargeGraph.vue";

const endpoint = defineModel<ExtendedEndpointDetails>({ required: true });
</script>

<template>
  <!--Throughput and retries-->
  <div class="col-xs-4 no-side-padding list-section graph-area graph-message-retries-throughputs">
    <!-- large graph -->
    <LargeGraph
      v-if="endpoint.metricDetails.metrics.throughput"
      :isdurationgraph="false"
      :firstdataseries="endpoint.metricDetails.metrics.throughput"
      :seconddataseries="endpoint.metricDetails.metrics.retries"
      :minimumyaxis="largeGraphsMinimumYAxis.throughputRetries"
      :firstseriestype="'throughput'"
      :secondseriestype="'retries'"
      :avgdecimals="0"
      :metricsuffix="'MSGS/S'"
    />
    <div class="no-side-padding graph-values">
      <div class="no-side-padding throughput-values">
        <div>
          <span class="metric-digest-header" v-tooltip :title="`Throughput: The number of messages per second successfully processed by a receiving endpoint.`"> Throughput </span>
        </div>
        <div class="metric-digest-value current">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">{{ formatGraphDecimalFromNumber(endpoint.digest.metrics.throughput?.latest, 2) }} <span class="metric-digest-value-suffix">MSGS/S</span></div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
        <div class="metric-digest-value average">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">{{ formatGraphDecimalFromNumber(endpoint.digest.metrics.throughput?.average, 2) }} <span class="metric-digest-value-suffix">MSGS/S AVG</span></div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
      </div>
      <div class="no-side-padding scheduled-retries-rate-values">
        <div>
          <span class="metric-digest-header" v-tooltip :title="`Scheduled retries: The number of messages per second scheduled for retries (immediate or delayed).`"> Scheduled retries </span>
        </div>

        <div class="metric-digest-value current">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">{{ formatGraphDecimalFromNumber(endpoint.digest.metrics.retries?.latest, 2) }} <span class="metric-digest-value-suffix">MSGS/S</span></div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
        <div class="metric-digest-value average">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">{{ formatGraphDecimalFromNumber(endpoint.digest.metrics.retries?.average, 2) }} <span class="metric-digest-value-suffix">MSGS/S AVG</span></div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "./monitoring.css";
@import "./largeGraphs.css";

.throughput-values span.metric-digest-header {
  color: var(--monitoring-throughput);
}

.throughput-values .current,
.throughput-values .average {
  border-color: var(--monitoring-throughput);
}

.scheduled-retries-rate-values span.metric-digest-header {
  color: var(--monitoring-retries);
}

.scheduled-retries-rate-values .current,
.scheduled-retries-rate-values .average {
  border-color: var(--monitoring-retries);
}
</style>
