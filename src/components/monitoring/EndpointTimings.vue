<script setup lang="ts">
import { computed } from "vue";
import { useFormatTime } from "@/composables/formatter";
import { largeGraphsMinimumYAxis } from "./formatGraph";
import LargeGraph from "./LargeGraph.vue";
import type { ExtendedEndpointDetails } from "@/resources/MonitoringEndpoint";

const endpoint = defineModel<ExtendedEndpointDetails>({
  required: true,
});

const latestProcessingTime = computed(() => useFormatTime(endpoint.value.digest.metrics.processingTime?.latest));
const averageProcessingTime = computed(() => useFormatTime(endpoint.value.digest.metrics.processingTime?.average));
const latestCriticalTime = computed(() => useFormatTime(endpoint.value.digest.metrics.criticalTime?.latest));
const averageCriticalTime = computed(() => useFormatTime(endpoint.value.digest.metrics.criticalTime?.average));
</script>

<template>
  <!--ProcessingTime and Critical Time-->
  <div class="col-xs-4 no-side-padding list-section graph-area graph-critical-processing-times">
    <!-- large graph -->
    <LargeGraph
      v-if="endpoint.metricDetails.metrics.criticalTime"
      :isdurationgraph="true"
      :firstdataseries="endpoint.metricDetails.metrics.criticalTime"
      :seconddataseries="endpoint.metricDetails.metrics.processingTime"
      :minimumyaxis="largeGraphsMinimumYAxis.processingCritical"
      :firstseriestype="'critical-time'"
      :secondseriestype="'processing-time'"
      :avgdecimals="0"
    />
    <div class="no-side-padding graph-values">
      <div class="no-side-padding processing-time-values">
        <div class="">
          <span class="metric-digest-header" v-tooltip :title="`Processing time: The time taken for a receiving endpoint to successfully process a message.`"> Processing Time </span>
        </div>
        <div class="metric-digest-value current">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">
            {{ latestProcessingTime.value }}
            <span class="metric-digest-value-suffix"> {{ latestProcessingTime.unit }}</span>
          </div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
        <div class="metric-digest-value average">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">
            {{ averageProcessingTime.value }}
            <span class="metric-digest-value-suffix"> {{ averageProcessingTime.unit }} AVG</span>
          </div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
      </div>

      <div class="no-side-padding critical-time-values">
        <div class="">
          <span class="metric-digest-header" v-tooltip :title="`Critical time: The elapsed time from when a message was sent, until it was successfully processed by a receiving endpoint.`"> Critical Time </span>
        </div>
        <div class="metric-digest-value current">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">
            <span :class="{ negative: parseFloat(latestCriticalTime.value) < 0 }"> {{ latestCriticalTime.value }}</span>
            <span class="metric-digest-value-suffix"> &nbsp;{{ latestCriticalTime.unit }}</span>
          </div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
        <div class="metric-digest-value average">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">
            <span :class="{ negative: parseFloat(averageCriticalTime.value) < 0 }"> {{ averageCriticalTime.value }}</span>
            <span class="metric-digest-value-suffix"> &nbsp;{{ averageCriticalTime.unit }} AVG </span>
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

.critical-time-values span.metric-digest-header {
  color: var(--monitoring-critical-time);
}

.critical-time-values .current,
.critical-time-values .average {
  border-color: var(--monitoring-critical-time);
}

.processing-time-values span.metric-digest-header {
  color: var(--monitoring-processing-time);
}

.processing-time-values .current,
.processing-time-values .average {
  border-color: var(--monitoring-processing-time);
}
</style>
