<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRouter, RouterLink } from "vue-router";
import { formatGraphDecimal, formatGraphDuration, smallGraphsMinimumYAxis } from "./formatGraph";
import { useDeleteFromMonitoring, useOptionsFromMonitoring } from "@/composables/serviceServiceControlUrls";
import { storeToRefs } from "pinia";
import { useMonitoringEndpointDetailsStore } from "@/stores/MonitoringEndpointDetailsStore";
import NoData from "@/components/NoData.vue";
import SmallGraph from "./SmallGraph.vue";
import type { ExtendedEndpointInstance } from "@/resources/MonitoringEndpoint";
import routeLinks from "@/router/routeLinks";
import ColumnHeader from "@/components/ColumnHeader.vue";

const isRemovingEndpointEnabled = ref<boolean>(false);
const router = useRouter();
const monitoringStore = useMonitoringEndpointDetailsStore();

const { endpointDetails: endpoint, endpointName } = storeToRefs(monitoringStore);

async function removeEndpoint(endpointName: string, instance: ExtendedEndpointInstance) {
  try {
    await useDeleteFromMonitoring("monitored-instance/" + endpointName + "/" + instance.id);
    endpoint.value.instances.splice(endpoint.value.instances.indexOf(instance), 1);
    if (endpoint.value.instances.length === 0) {
      router.push(routeLinks.monitoring.root);
    }
  } catch (err) {
    console.log(err);
    return false;
  }
}

async function getIsRemovingEndpointEnabled() {
  try {
    const response = await useOptionsFromMonitoring();
    if (response) {
      const headers = response.headers;
      const allow = headers.get("Allow");
      if (allow) {
        const deleteAllowed = allow.indexOf("DELETE") >= 0;
        return deleteAllowed;
      }
    }
  } catch (err) {
    console.log(err);
  }
  return false;
}

onMounted(async () => {
  isRemovingEndpointEnabled.value = await getIsRemovingEndpointEnabled();
});
</script>

<template>
  <div class="row">
    <div role="table" aria-label="instances" class="col-xs-12 no-side-padding">
      <!-- Breakdown by instance-->
      <!--headers-->
      <div role="row" aria-label="instances-column-headers" class="row box box-no-click table-head-row">
        <ColumnHeader name="instance-name" label="Instance Name" column-class="col-xs-4 col-xl-8" />
        <ColumnHeader name="throughput" label="Throughput" unit="(msgs/s)" column-class="col-xs-2 col-xl-1 no-side-padding">
          <template #help>Throughput: The number of messages per second successfully processed by a receiving endpoint.</template>
        </ColumnHeader>
        <ColumnHeader name="retires" label="Scheduled retries" unit="(msgs/s)" column-class="col-xs-2 col-xl-1 no-side-padding">
          <template #help>Scheduled retries: The number of messages per second scheduled for retries (immediate or delayed).</template>
        </ColumnHeader>
        <ColumnHeader name="processing-time" label="Processing time" unit="(t)" column-class="col-xs-2 col-xl-1 no-side-padding">
          <template #help>Processing time: The time taken for a receiving endpoint to successfully process a message.</template>
        </ColumnHeader>
        <ColumnHeader name="critical-time" label="Critical time" unit="(t)" column-class="col-xs-2 col-xl-1 no-side-padding">
          <template #help>Critical time: The elapsed time from when a message was sent, until it was successfully processed by a receiving endpoint.</template>
        </ColumnHeader>
      </div>

      <NoData v-if="!endpoint?.instances?.length" title="No messages" message="No messages processed in this period of time"></NoData>

      <div role="rowgroup" class="row endpoint-instances">
        <div class="col-xs-12 no-side-padding">
          <div class="row box endpoint-row" v-for="(instance, id) in endpoint.instances" :key="id">
            <div class="col-xs-12 no-side-padding">
              <div role="row" :aria-label="instance.name" class="row">
                <div role="cell" aria-label="instance-name" class="col-xs-4 col-xl-8 endpoint-name">
                  <div class="box-header with-status">
                    <div role="instance-name" aria-label="instance-name" class="no-side-padding lead righ-side-ellipsis" v-tippy="instance.name">
                      {{ instance.name }}
                    </div>
                    <div class="no-side-padding endpoint-status">
                      <span role="status" aria-label="negative-critical-time-warning" class="warning" v-if="parseFloat(formatGraphDuration(instance.metrics.criticalTime).value) < 0">
                        <i class="fa pa-warning" v-tippy="`Warning: instance currently has negative critical time, possibly because of a clock drift.`"></i>
                      </span>
                      <span role="status" aria-label="disconnected-warning" class="warning" v-if="instance.isScMonitoringDisconnected">
                        <i class="fa pa-monitoring-lost endpoint-details" v-tippy="`Unable to connect to monitoring server`"></i>
                      </span>
                      <span role="status" aria-label="stale-warning" class="warning" v-if="instance.isStale">
                        <i class="fa pa-endpoint-lost endpoint-details" v-tippy="`Not receiving metrics from this instance. Instance will be removed automatically.`"></i>
                      </span>
                      <span role="status" aria-label="error-count-warning" class="warning" v-if="instance.errorCount" v-tippy="`${instance.errorCount} failed messages associated with this endpoint. Click to see list.`">
                        <RouterLink :to="routeLinks.failedMessage.group.link(instance.serviceControlId)" v-if="instance.errorCount" class="warning cursorpointer">
                          <i class="fa fa-envelope"></i>
                          <span aria-label="error-count" class="badge badge-important cursorpointer"> {{ instance.errorCount }}</span>
                        </RouterLink>
                      </span>
                    </div>
                  </div>
                </div>
                <div role="cell" aria-label="throughput" class="col-xs-2 col-xl-1 no-side-padding">
                  <div class="row box-header">
                    <div class="no-side-padding">
                      <SmallGraph :type="'throughput'" :isdurationgraph="false" :plotdata="instance.metrics.throughput" :minimumyaxis="smallGraphsMinimumYAxis.throughput" :metricsuffix="'MSGS/S'" />
                      <span class="no-side-padding sparkline-value">
                        {{ instance.isStale == true || instance.isScMonitoringDisconnected == true ? "" : formatGraphDecimal(instance.metrics.throughput) }}
                        <strong v-if="instance.isStale && !instance.isScMonitoringDisconnected" v-tippy="`No metrics received or instance is not configured to send metrics`">?</strong>
                        <strong v-if="instance.isScMonitoringDisconnected" v-tippy="`Unable to connect to monitoring server`">?</strong>
                      </span>
                    </div>
                  </div>
                </div>
                <div role="cell" aria-label="retires" class="col-xs-2 col-xl-1 no-side-padding">
                  <div class="row box-header">
                    <div class="no-side-padding">
                      <SmallGraph :type="'retries'" :isdurationgraph="false" :plotdata="instance.metrics.retries" :minimumyaxis="smallGraphsMinimumYAxis.retries" :metricsuffix="'MSGS/S'" />
                      <span class="no-side-padding sparkline-value">
                        {{ instance.isStale == true || instance.isScMonitoringDisconnected == true ? "" : formatGraphDecimal(instance.metrics.retries) }}
                        <strong v-if="instance.isStale && !instance.isScMonitoringDisconnected" v-tippy="`No metrics received or instance is not configured to send metrics`">?</strong>
                        <strong v-if="instance.isScMonitoringDisconnected" v-tippy="`Unable to connect to monitoring server`">?</strong>
                      </span>
                    </div>
                  </div>
                </div>
                <div role="cell" aria-label="processing-time" class="col-xs-2 col-xl-1 no-side-padding">
                  <div class="row box-header">
                    <div class="no-side-padding">
                      <SmallGraph :type="'processing-time'" :isdurationgraph="true" :plotdata="instance.metrics.processingTime" :minimumyaxis="smallGraphsMinimumYAxis.processingTime" />
                      <span class="no-side-padding sparkline-value">
                        {{ instance.isStale == true || instance.isScMonitoringDisconnected == true ? "" : formatGraphDuration(instance.metrics.processingTime).value }}
                        <strong v-if="instance.isStale && !instance.isScMonitoringDisconnected" v-tippy="`No metrics received or instance is not configured to send metrics`">?</strong>
                        <strong v-if="instance.isScMonitoringDisconnected" v-tippy="`Unable to connect to monitoring server`">?</strong>
                        <span v-if="!instance.isStale && !instance.isScMonitoringDisconnected" class="unit">
                          {{ formatGraphDuration(instance.metrics.processingTime).unit }}
                        </span>
                      </span>
                    </div>
                  </div>
                </div>
                <div role="cell" aria-label="critical-time" class="col-xs-2 col-xl-1 no-side-padding">
                  <div class="row box-header">
                    <div class="no-side-padding">
                      <SmallGraph :type="'critical-time'" :isdurationgraph="true" :plotdata="instance.metrics.criticalTime" :minimumyaxis="smallGraphsMinimumYAxis.criticalTime" />
                      <span class="no-side-padding sparkline-value" :class="{ negative: parseFloat(formatGraphDuration(instance.metrics.criticalTime).value) < 0 }">
                        {{ instance.isStale == true || instance.isScMonitoringDisconnected == true ? "" : formatGraphDuration(instance.metrics.criticalTime).value }}
                        <strong v-if="instance.isStale && !instance.isScMonitoringDisconnected" v-tippy="`No metrics received or instance is not configured to send metrics`">?</strong>
                        <strong v-if="instance.isScMonitoringDisconnected" v-tippy="`Unable to connect to monitoring server`">?</strong>
                        <span v-if="!instance.isStale && !instance.isScMonitoringDisconnected" class="unit">
                          {{ formatGraphDuration(instance.metrics.criticalTime).unit }}
                        </span>
                      </span>
                    </div>
                  </div>
                </div>

                <!--remove endpoint-->
                <div class="col-xs-2 col-xl-1 no-side-padding">
                  <a v-if="isRemovingEndpointEnabled && instance.isStale" class="remove-endpoint" @click="removeEndpoint(endpointName, instance)">
                    <i class="fa fa-trash" v-tippy="`Remove endpoint`"></i>
                  </a>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "@/components/list.css";
@import "./endpoint.css";
@import "./endpointSubTab.css";

.endpoint-row a.remove-endpoint {
  display: block;
  position: absolute;
  top: 17px;
  right: 22px;
}

.endpoint-row:hover a.remove-endpoint {
  display: block;
  position: absolute;
  top: 17px;
  right: 22px;
}

a.remove-endpoint {
  margin-left: 7px;
}

a.remove-endpoint:hover {
  cursor: pointer;
}

a.remove-endpoint i {
  color: #00a3c4;
}

a.remove-endpoint:hover i {
  color: #00729c;
}

.pa-warning {
  padding-top: 25px;
}
</style>
