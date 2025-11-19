<script setup lang="ts">
import { computed } from "vue";
import { useFormatTime } from "@/composables/formatter";
import { largeGraphsMinimumYAxis } from "./formatGraph";
import LargeGraph from "./LargeGraph.vue";
import type { ExtendedEndpointDetails } from "@/resources/MonitoringEndpoint";
import { CriticalTime, ProcessingTime } from "@/resources/MonitoringResources";

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
  <div role="gridcell" aria-label="timings-data" class="col-xs-4 no-side-padding list-section graph-area graph-critical-processing-times">
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
      <div aria-label="processing-time-values" class="no-side-padding processing-time-values">
        <div aria-label="metric-header">
          <span class="metric-digest-header" v-tippy="ProcessingTime.tooltip">{{ ProcessingTime.label }}</span>
        </div>
        <div aria-label="metric-current-value" class="metric-digest-value current">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">
            {{ latestProcessingTime.value }}
            <span class="metric-digest-value-suffix"> {{ latestProcessingTime.unit }}</span>
          </div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
        <div aria-label="metric-average-value" class="metric-digest-value average">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">
            {{ averageProcessingTime.value }}
            <span class="metric-digest-value-suffix"> {{ averageProcessingTime.unit }} AVG</span>
          </div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
      </div>

      <div aria-label="critical-time-values" class="no-side-padding critical-time-values">
        <div aria-label="metric-header">
          <span class="metric-digest-header" v-tippy="CriticalTime.tooltip">{{ CriticalTime.label }}</span>
        </div>
        <div aria-label="metric-current-value" class="metric-digest-value current">
          <div v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected">
            <span :class="{ negative: parseFloat(latestCriticalTime.value) < 0 }"> {{ latestCriticalTime.value }}</span>
            <span class="metric-digest-value-suffix"> &nbsp;{{ latestCriticalTime.unit }}</span>
          </div>
          <strong v-if="endpoint.isStale || endpoint.isScMonitoringDisconnected">?</strong>
        </div>
        <div aria-label="metric-average-value" class="metric-digest-value average">
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
