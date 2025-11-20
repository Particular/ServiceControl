<script setup lang="ts">
import { ref, computed } from "vue";
import { RouterLink } from "vue-router";
import { smallGraphsMinimumYAxis, formatGraphDuration, formatGraphDecimal } from "./formatGraph";
import SmallGraph from "./SmallGraph.vue";
import { useMonitoringHistoryPeriodStore } from "@/stores/MonitoringHistoryPeriodStore";
import { useMonitoringStore } from "../../stores/MonitoringStore";
import { storeToRefs } from "pinia";
import type { GroupedEndpoint, Endpoint } from "@/resources/MonitoringEndpoint";
import routeLinks from "@/router/routeLinks";
import { Tippy } from "vue-tippy";
import { CriticalTime, EndpointName, ProcessingTime, QueueLength, ScheduledRetries, Throughput } from "@/resources/MonitoringResources";
import FAIcon from "@/components/FAIcon.vue";
import { faEnvelope } from "@fortawesome/free-solid-svg-icons";

const settings = defineProps<{
  endpoint: GroupedEndpoint | Endpoint;
}>();

const monitoringHistoryPeriodStore = useMonitoringHistoryPeriodStore();
const monitoringStore = useMonitoringStore();
const isGrouped = computed<boolean>(() => monitoringStore.endpointListIsGrouped);
const endpoint = computed<Endpoint>(() => {
  return isGrouped.value ? (settings.endpoint as GroupedEndpoint).endpoint : (settings.endpoint as Endpoint);
});
const shortName = computed(() => {
  return isGrouped.value ? (settings.endpoint as GroupedEndpoint).shortName : endpoint.value.name;
});
const supportsEndpointCount = ref();
const { historyPeriod } = storeToRefs(monitoringHistoryPeriodStore);

const processingTimeGraphDuration = computed(() => formatGraphDuration(endpoint.value.metrics.processingTime));
const criticalTimeGraphDuration = computed(() => formatGraphDuration(endpoint.value.metrics.criticalTime));
</script>

<template>
  <div role="gridcell" :aria-label="EndpointName.name" class="table-first-col endpoint-name name-overview">
    <div class="box-header with-status">
      <div :aria-label="shortName" class="no-side-padding lead righ-side-ellipsis endpoint-details-link">
        <tippy :aria-label="endpoint.name" :delay="[700, 0]">
          <template #content>
            <p :style="{ overflowWrap: 'break-word' }">{{ endpoint.name }}</p>
          </template>
          <RouterLink class="cursorpointer hackToPreventSafariFromShowingTooltip" aria-label="details-link" :to="routeLinks.monitoring.endpointDetails.link(endpoint.name, historyPeriod.pVal)">
            {{ shortName }}
          </RouterLink>
        </tippy>
      </div>
      <span aria-label="instances-connected-total" class="endpoint-count" v-if="endpoint.connectedCount || endpoint.disconnectedCount" v-tippy="'Endpoint instance(s): (connected/total)'">
        ({{ endpoint.connectedCount }}/{{ endpoint.connectedCount + endpoint.disconnectedCount }})</span
      >
      <div class="no-side-padding endpoint-status">
        <span role="status" class="warning" v-if="endpoint.metrics != null && parseFloat(criticalTimeGraphDuration.value) < 0">
          <i class="fa pa-warning" v-tippy="'Warning: endpoint currently has negative critical time, possibly because of a clock drift.'"></i>
        </span>
        <span role="status" class="warning" v-if="endpoint.isScMonitoringDisconnected">
          <i class="fa pa-monitoring-lost endpoints-overview" v-tippy="'Unable to connect to monitoring server'"></i>
        </span>
        <span role="status" class="warning" v-if="(endpoint.isStale && !supportsEndpointCount) || !endpoint.connectedCount" v-tippy="'No data received from any instance'">
          <RouterLink :to="routeLinks.monitoring.endpointDetails.link(endpoint.name, historyPeriod.pVal, 'instancesBreakdown')" class="cursorpointer">
            <i role="img" aria-label="endpoint-no-data" class="fa pa-endpoint-lost endpoints-overview" />
          </RouterLink>
        </span>
        <span role="status" class="warning" v-if="endpoint.errorCount" v-tippy="endpoint.errorCount + ` failed messages associated with this endpoint. Click to see list.`">
          <RouterLink :to="routeLinks.failedMessage.group.link(endpoint.serviceControlId)" v-if="endpoint.errorCount" class="warning cursorpointer">
            <FAIcon :icon="faEnvelope" class="endpoint-status-icon" />
            <span class="badge badge-important ng-binding cursorpointer">{{ endpoint.errorCount }}</span>
          </RouterLink>
        </span>
      </div>
    </div>
  </div>
  <!--Queue Length-->
  <div role="gridcell" :aria-label="QueueLength.name" class="table-col">
    <div class="box-header">
      <div class="no-side-padding">
        <SmallGraph
          role="img"
          :aria-label="QueueLength.name"
          :type="'queue-length'"
          :isdurationgraph="false"
          :plotdata="endpoint.metrics.queueLength"
          :minimumyaxis="smallGraphsMinimumYAxis.queueLength"
          :avglabelcolor="'#EA7E00'"
          :metricsuffix="'MSGS'"
        />
      </div>
      <div role="text" aria-label="sparkline" class="no-side-padding sparkline-value">
        {{ endpoint.isStale == true || endpoint.isScMonitoringDisconnected == true ? "" : formatGraphDecimal(endpoint.metrics.queueLength, 0) }}
        <strong v-if="endpoint.isStale && !endpoint.isScMonitoringDisconnected" v-tippy="'No metrics received or endpoint is not configured to send metrics'">?</strong>
        <strong v-if="endpoint.isScMonitoringDisconnected" v-tippy="'Unable to connect to monitoring server'">?</strong>
      </div>
    </div>
  </div>
  <!--Throughput-->
  <div role="gridcell" :aria-label="Throughput.name" class="table-col">
    <div class="box-header">
      <div class="no-side-padding">
        <SmallGraph
          role="img"
          :aria-label="Throughput.name"
          :type="'throughput'"
          :isdurationgraph="false"
          :plotdata="endpoint.metrics.throughput"
          :minimumyaxis="smallGraphsMinimumYAxis.throughput"
          :avglabelcolor="'#176397'"
          :metricsuffix="'MSGS/S'"
        />
      </div>
      <div role="text" aria-label="sparkline" class="no-side-padding sparkline-value">
        {{ endpoint.isStale == true || endpoint.isScMonitoringDisconnected == true ? "" : formatGraphDecimal(endpoint.metrics.throughput, 2) }}
        <strong v-if="endpoint.isStale && !endpoint.isScMonitoringDisconnected" v-tippy="'No metrics received or endpoint is not configured to send metrics'">?</strong>
        <strong v-if="endpoint.isScMonitoringDisconnected" v-tippy="'Unable to connect to monitoring server'">?</strong>
      </div>
    </div>
  </div>
  <!--Scheduled Retries-->
  <div role="gridcell" :aria-label="ScheduledRetries.name" class="table-col">
    <div class="box-header">
      <div class="no-side-padding">
        <SmallGraph role="img" :aria-label="ScheduledRetries.name" :type="'retries'" :isdurationgraph="false" :plotdata="endpoint.metrics.retries" :minimumyaxis="smallGraphsMinimumYAxis.retries" :avglabelcolor="'#CC1252'" :metricsuffix="'MSGS/S'" />
      </div>
      <div role="text" aria-label="sparkline" class="no-side-padding sparkline-value">
        {{ endpoint.isStale == true || endpoint.isScMonitoringDisconnected == true ? "" : formatGraphDecimal(endpoint.metrics.retries, 2) }}
        <strong v-if="endpoint.isStale && !endpoint.isScMonitoringDisconnected" v-tippy="'No metrics received or endpoint is not configured to send metrics'">?</strong>
        <strong v-if="endpoint.isScMonitoringDisconnected" v-tippy="'Unable to connect to monitoring server'">?</strong>
      </div>
    </div>
  </div>
  <!--Processing Time-->
  <div role="gridcell" :aria-label="ProcessingTime.name" class="table-col">
    <div class="box-header">
      <div class="no-side-padding">
        <SmallGraph role="img" :aria-label="ProcessingTime.name" :type="'processing-time'" :isdurationgraph="true" :plotdata="endpoint.metrics.processingTime" :minimumyaxis="smallGraphsMinimumYAxis.processingTime" :avglabelcolor="'#258135'" />
      </div>
      <div role="text" aria-label="sparkline" class="no-side-padding sparkline-value">
        {{ endpoint.isStale == true || endpoint.isScMonitoringDisconnected == true ? "" : processingTimeGraphDuration.value }}
        <strong v-if="endpoint.isStale && !endpoint.isScMonitoringDisconnected" v-tippy="'No metrics received or endpoint is not configured to send metrics'">?</strong>
        <strong v-if="endpoint.isScMonitoringDisconnected" v-tippy="'Unable to connect to monitoring server'">?</strong>
        <span v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected"> {{ processingTimeGraphDuration.unit }}</span>
      </div>
    </div>
  </div>
  <!--Critical Time-->
  <div role="gridcell" :aria-label="CriticalTime.name" class="table-col">
    <div class="box-header">
      <div class="no-side-padding">
        <SmallGraph role="img" :aria-label="CriticalTime.name" :type="'critical-time'" :isdurationgraph="true" :plotdata="endpoint.metrics.criticalTime" :minimumyaxis="smallGraphsMinimumYAxis.criticalTime" :avglabelcolor="'#2700CB'" />
      </div>
      <div role="text" aria-label="sparkline" class="no-side-padding sparkline-value" :class="{ negative: parseFloat(criticalTimeGraphDuration.value) < 0 }">
        {{ endpoint.isStale == true || endpoint.isScMonitoringDisconnected == true ? "" : criticalTimeGraphDuration.value }}
        <strong v-if="endpoint.isStale && !endpoint.isScMonitoringDisconnected" title="No metrics received or endpoint is not configured to send metrics">?</strong>
        <strong v-if="endpoint.isScMonitoringDisconnected" title="Unable to connect to monitoring server">?</strong>
        <span v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected" class="unit"> {{ criticalTimeGraphDuration.unit }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "../list.css";
@import "./monitoring.css";
@import "./endpoint.css";
@import "./endpointTables.css";

.hackToPreventSafariFromShowingTooltip::after {
  content: "";
  display: block;
}

.lead.endpoint-details-link.righ-side-ellipsis {
  color: #00729c;
  margin: 0;
}

.monitoring-lost-link i {
  top: 7px;
}

.endpoint-count {
  font-weight: bold;
}

.pa-warning {
  padding-top: 25px;
}
</style>
