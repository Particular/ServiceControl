<script setup lang="ts">
import type { ExtendedEndpointDetails } from "@/resources/MonitoringEndpoint";
import { formatGraphDecimalFromNumber, largeGraphsMinimumYAxis } from "./formatGraph";
import LargeGraph from "./LargeGraph.vue";
import { QueueLength } from "@/resources/MonitoringResources";

const endpoint = defineModel<ExtendedEndpointDetails>({
  required: true,
});
</script>

<template>
  <div role="gridcell" aria-label="backlog-data" class="col-xs-4 no-side-padding list-section graph-area graph-queue-length">
    <!-- large graph -->
    <LargeGraph
      v-if="endpoint.metricDetails.metrics.queueLength"
      :isdurationgraph="false"
      :firstdataseries="endpoint.metricDetails.metrics.queueLength"
      :minimumyaxis="largeGraphsMinimumYAxis.queueLength"
      :firstseriestype="'queue-length'"
      :avgdecimals="0"
      :metricsuffix="'MSGS'"
    />
    <!--Queue Length-->
    <div class="no-side-padding graph-values">
      <div aria-label="queue-length-values" class="queue-length-values">
        <div aria-label="metric-header">
          <span class="metric-digest-header" v-tippy="QueueLength.tooltip">{{ QueueLength.label }}</span>
        </div>
        <div aria-label="metric-current-value" class="metric-digest-value current">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">
            {{ formatGraphDecimalFromNumber(endpoint.digest.metrics.queueLength?.latest, 0) }} <span v-if="!endpoint.isStale || !endpoint.isScMonitoringDisconnected" class="metric-digest-value-suffix">MSGS</span>
          </div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
        <div aria-label="metric-average-value" class="metric-digest-value average">
          <div aria-label="graph-average" v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">
            {{ formatGraphDecimalFromNumber(endpoint.digest.metrics.queueLength?.average, 0) }} <span class="metric-digest-value-suffix">MSGS AVG</span>
          </div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "./monitoring.css";
@import "./largeGraphs.css";

.graph-queue-length .metric-digest-value {
  flex-basis: 100%;
}

.queue-length-values {
  display: inline-block;
}

.queue-length-values .metric-digest-header {
  color: var(--monitoring-queue-length);
}

.graph-queue-length .current,
.graph-queue-length .average {
  border-color: var(--monitoring-queue-length);
}
</style>
