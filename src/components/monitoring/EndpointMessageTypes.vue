<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { formatGraphDecimal, formatGraphDuration, smallGraphsMinimumYAxis } from "./formatGraph";
import { storeToRefs } from "pinia";

import NoData from "@/components/NoData.vue";
import SmallGraph from "./SmallGraph.vue";
import PaginationStrip from "@/components/PaginationStrip.vue";
import { useMonitoringEndpointDetailsStore } from "@/stores/MonitoringEndpointDetailsStore";
import ColumnHeader from "@/components/ColumnHeader.vue";
import { CriticalTime, MessageType, ProcessingTime, ScheduledRetries, Throughput } from "@/resources/MonitoringResources";
import FAIcon from "@/components/FAIcon.vue";
import { faWarning } from "@fortawesome/free-solid-svg-icons";

const monitoringStore = useMonitoringEndpointDetailsStore();
const { endpointDetails: endpoint, messageTypes, messageTypesAvailable } = storeToRefs(monitoringStore);

const route = useRoute();
const router = useRouter();
const messageTypesPage = ref(Number(route?.query?.pageNo ?? "1"));

watch(messageTypesPage, () => {
  router.replace({ query: { ...route.query, pageNo: messageTypesPage.value } });
});

const props = defineProps({
  perPage: {
    type: Number,
    default: 10,
  },
});

const paginatedMessageTypes = computed(() => {
  const pageStart = (messageTypesPage.value - 1) * props.perPage;
  const pageEnd = messageTypesPage.value * props.perPage;
  return messageTypes.value ? messageTypes.value.data.slice(pageStart, pageEnd) : [];
});
</script>

<template>
  <div class="row">
    <div role="table" aria-label="message-types" class="col-xs-12 no-side-padding">
      <div v-if="messageTypesAvailable" class="alert alert-warning endpoint-data-changed">
        <FAIcon :icon="faWarning" /> <strong>Warning:</strong> The number of available message types has changed.
        <a @click="monitoringStore.updateMessageTypes()" class="alink">Click here to reload the view</a>
      </div>

      <!-- Breakdown by message type-->
      <!--headers-->
      <div role="row" aria-label="message-type-column-headers" class="row box box-no-click table-head-row">
        <ColumnHeader :name="MessageType.name" :label="MessageType.label" class="col-xs-4 col-xl-8" />
        <ColumnHeader :name="Throughput.name" :label="Throughput.label" :unit="Throughput.unit" class="col-xs-2 col-xl-1 no-side-padding">
          <template #help>{{ Throughput.tooltip }}</template>
        </ColumnHeader>
        <ColumnHeader :name="ScheduledRetries.name" :label="ScheduledRetries.label" :unit="ScheduledRetries.unit" class="col-xs-2 col-xl-1 no-side-padding">
          <template #help>{{ ScheduledRetries.tooltip }}</template>
        </ColumnHeader>
        <ColumnHeader :name="ProcessingTime.name" :label="ProcessingTime.label" :unit="ProcessingTime.unit" class="col-xs-2 col-xl-1 no-side-padding">
          <template #help>{{ ProcessingTime.tooltip }}</template>
        </ColumnHeader>
        <ColumnHeader :name="CriticalTime.name" :label="CriticalTime.label" :unit="CriticalTime.unit" class="col-xs-2 col-xl-1 no-side-padding">
          <template #help>{{ CriticalTime.tooltip }}</template>
        </ColumnHeader>
      </div>

      <no-data v-if="!endpoint?.messageTypes?.length" message="No messages processed in this period of time."></no-data>

      <div role="rowgroup" aria-label="message-type-rows" class="row">
        <div class="col-xs-12 no-side-padding">
          <div class="row box endpoint-row" v-for="messageType in paginatedMessageTypes" :key="messageType.id">
            <div class="col-xs-12 no-side-padding">
              <div role="row" :aria-label="messageType.shortName" class="row">
                <div role="cell" class="col-xs-4 col-xl-8 endpoint-name" :title="messageType?.tooltipText">
                  <div class="box-header with-status">
                    <div role="message-type-name" aria-label="message-type-name" class="col-lg-max-9 no-side-padding lead message-type-label righ-side-ellipsis">
                      <div class="lead">
                        {{ messageType?.shortName || "Unknown" }}
                      </div>
                    </div>
                    <div class="no-side-padding endpoint-status message-type-status">
                      <span class="warning" v-if="messageType.metrics != null && parseFloat(formatGraphDuration(messageType.metrics.criticalTime).value) < 0">
                        <i class="fa pa-warning" v-tippy="`Warning: message type currently has negative critical time, possibly because of a clock drift.`"></i>
                      </span>
                      <span class="warning" v-if="endpoint.isScMonitoringDisconnected">
                        <i class="fa pa-monitoring-lost endpoint-details" v-tippy="`Unable to connect to monitoring server`"></i>
                      </span>
                    </div>
                  </div>
                  <div aria-label="message-type-properties" class="row message-type-properties">
                    <div v-if="messageType.typeName && messageType.typeName != 'null' && !messageType.containsTypeHierarchy" class="message-type-part">
                      {{ messageType.assemblyName + "-" + messageType.assemblyVersion }}
                    </div>
                    <div class="message-type-part" v-for="(type, id) in messageType.messageTypeHierarchy" :key="id">
                      <span v-if="messageType.typeName && messageType.typeName != 'null' && messageType.containsTypeHierarchy"> {{ type.assemblyName + "-" + type.assemblyVersion }}</span>
                    </div>
                    <div v-if="messageType.culture && messageType.culture != 'null'" class="message-type-part">{{ "Culture=" + messageType.culture }}</div>
                    <div v-if="messageType.publicKeyToken && messageType.publicKeyToken != 'null'" class="message-type-part">{{ "PublicKeyToken=" + messageType.publicKeyToken }}</div>
                  </div>
                </div>
                <div role="cell" aria-label="throughput" class="col-xs-2 col-xl-1 no-side-padding">
                  <div class="row box-header">
                    <div class="no-side-padding">
                      <SmallGraph :type="'throughput'" :isdurationgraph="false" :plotdata="messageType.metrics.throughput" :minimumyaxis="smallGraphsMinimumYAxis.throughput" :metricsuffix="'MSGS/S'" />
                      <span class="no-side-padding sparkline-value">
                        {{ endpoint.isStale == true || endpoint.isScMonitoringDisconnected == true ? "" : formatGraphDecimal(messageType.metrics.throughput, 2) }}
                        <strong v-if="endpoint.isStale && !endpoint.isScMonitoringDisconnected" v-tippy="`No metrics received or endpoint is not configured to send metrics`">?</strong>
                        <strong v-if="endpoint.isScMonitoringDisconnected" v-tippy="`Unable to connect to monitoring server`">?</strong>
                      </span>
                    </div>
                  </div>
                </div>
                <div role="cell" aria-label="retires" class="col-xs-2 col-xl-1 no-side-padding">
                  <div class="row box-header">
                    <div class="no-side-padding">
                      <SmallGraph :type="'retries'" :isdurationgraph="false" :plotdata="messageType.metrics.retries" :minimumyaxis="smallGraphsMinimumYAxis.retries" :metricsuffix="'MSGS/S'" />
                      <span class="no-side-padding sparkline-value">
                        {{ endpoint.isStale == true || endpoint.isScMonitoringDisconnected == true ? "" : formatGraphDecimal(messageType.metrics.retries, 2) }}
                        <strong v-if="endpoint.isStale && !endpoint.isScMonitoringDisconnected" v-tippy="`No metrics received or endpoint is not configured to send metrics`">?</strong>
                        <strong v-if="endpoint.isScMonitoringDisconnected" v-tippy="`Unable to connect to monitoring server`">?</strong>
                      </span>
                    </div>
                  </div>
                </div>
                <div role="cell" aria-label="processing-time" class="col-xs-2 col-xl-1 no-side-padding">
                  <div class="row box-header">
                    <div class="no-side-padding">
                      <SmallGraph :type="'processing-time'" :isdurationgraph="true" :plotdata="messageType.metrics.processingTime" :minimumyaxis="smallGraphsMinimumYAxis.processingTime" />
                      <span class="no-side-padding sparkline-value">
                        {{ endpoint.isStale == true || endpoint.isScMonitoringDisconnected == true ? "" : formatGraphDuration(messageType.metrics.processingTime).value }}
                        <strong v-if="endpoint.isStale && !endpoint.isScMonitoringDisconnected" v-tippy="`No metrics received or endpoint is not configured to send metrics`">?</strong>
                        <strong v-if="endpoint.isScMonitoringDisconnected" v-tippy="`Unable to connect to monitoring server`">?</strong>
                        <span v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected" class="unit">
                          {{ formatGraphDuration(messageType.metrics.processingTime).unit }}
                        </span>
                      </span>
                    </div>
                  </div>
                </div>
                <div role="cell" aria-label="critical-time" class="col-xs-2 col-xl-1 no-side-padding">
                  <div class="row box-header">
                    <div class="no-side-padding">
                      <SmallGraph :type="'critical-time'" :isdurationgraph="true" :plotdata="messageType.metrics.criticalTime" :minimumyaxis="smallGraphsMinimumYAxis.criticalTime" />
                      <span class="no-side-padding sparkline-value" :class="{ negative: parseFloat(formatGraphDuration(messageType.metrics.criticalTime).value) < 0 }">
                        {{ endpoint.isStale == true || endpoint.isScMonitoringDisconnected == true ? "" : formatGraphDuration(messageType.metrics.criticalTime).value }}
                        <strong v-if="endpoint.isStale && !endpoint.isScMonitoringDisconnected" v-tippy="`No metrics received or endpoint is not configured to send metrics`">?</strong>
                        <strong v-if="endpoint.isScMonitoringDisconnected" v-tippy="`Unable to connect to monitoring server`">?</strong>
                        <span v-if="!endpoint.isStale && !endpoint.isScMonitoringDisconnected" class="unit">
                          {{ formatGraphDuration(messageType.metrics.criticalTime).unit }}
                        </span>
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
      <PaginationStrip v-model="messageTypesPage" :itemsPerPage="perPage" :totalCount="messageTypes?.data?.length ?? 0" />
    </div>
  </div>
</template>

<style scoped>
@import "@/components/list.css";
@import "./endpoint.css";
@import "./endpointSubTab.css";

.message-type-part {
  margin-right: 24px;
  color: #8c8c8c;
  font-weight: normal;
  font-size: 12px;
  display: inline-block;
}

.row.message-type-properties {
  position: relative;
  top: -5px;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.endpoint-data-changed {
  text-align: center;
  margin: 26px 0 0;
}

.endpoint-data-changed a {
  text-decoration: underline;
}

.endpoint-data-changed a:hover {
  cursor: pointer;
}

.endpoint-data-changed.sticky {
  position: fixed;
  top: 50px;
  width: 92%;
  z-index: 999999;
  box-shadow: 0 3px 20px rgba(0, 0, 0, 0.15);
  transition-duration: 0.5s;
}

.pa-warning {
  padding-top: 25px;
}
</style>
