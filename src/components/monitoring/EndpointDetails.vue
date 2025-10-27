<script setup lang="ts">
// Composables
import { computed, watch, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter, RouterLink } from "vue-router";
import { storeToRefs } from "pinia";
//stores
import { useMonitoringEndpointDetailsStore } from "../../stores/MonitoringEndpointDetailsStore";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";
// Components
import LicenseNotExpired from "../../components/LicenseNotExpired.vue";
import ServiceControlAvailable from "../../components/ServiceControlAvailable.vue";
import MonitoringNotAvailable from "./MonitoringNotAvailable.vue";
import PeriodSelector from "./MonitoringHistoryPeriod.vue";
import EndpointBacklog from "./EndpointBacklog.vue";
import EndpointWorkload from "./EndpointWorkload.vue";
import EndpointTimings from "./EndpointTimings.vue";
import EndpointInstances from "./EndpointInstances.vue";
import EndpointMessageTypes from "./EndpointMessageTypes.vue";
import { useMonitoringHistoryPeriodStore } from "@/stores/MonitoringHistoryPeriodStore";
import routeLinks from "@/router/routeLinks";
import FAIcon from "@/components/FAIcon.vue";
import { faEnvelope } from "@fortawesome/free-solid-svg-icons";
import { useServiceControlStore } from "@/stores/ServiceControlStore";

const { store: connectionStore } = useConnectionsAndStatsAutoRefresh();
const monitoringConnectionState = connectionStore.monitoringConnectionState;

const route = useRoute();
const router = useRouter();
const endpointName = route.params.endpointName.toString();
let refreshInterval: number;

const monitoringStore = useMonitoringEndpointDetailsStore();
const monitoringHistoryPeriodStore = useMonitoringHistoryPeriodStore();
const serviceControlStore = useServiceControlStore();
const { isMonitoringDisabled } = storeToRefs(serviceControlStore);

const { historyPeriod } = storeToRefs(monitoringHistoryPeriodStore);
const { negativeCriticalTimeIsPresent, endpointDetails: endpoint } = storeToRefs(monitoringStore);

watch(historyPeriod, (newValue) => {
  changeRefreshInterval(newValue.refreshIntervalVal);
});

const tabs = Object.freeze({
  messageTypeBreakdown: "messageTypeBreakdown",
  instancesBreakdown: "instancesBreakdown",
});

const activeTab = computed({
  get() {
    return route?.query?.tab ?? tabs.messageTypeBreakdown;
  },
  set(newValue) {
    router.replace({ query: { ...route.query, tab: newValue } });
  },
});

async function getEndpointDetails() {
  await monitoringStore.getEndpointDetails(endpointName);
}

function changeRefreshInterval(milliseconds: number) {
  if (typeof refreshInterval !== "undefined") {
    clearInterval(refreshInterval);
  }
  getEndpointDetails();
  refreshInterval = window.setInterval(() => {
    getEndpointDetails();
  }, milliseconds);
}
onUnmounted(() => {
  if (typeof refreshInterval !== "undefined") {
    clearInterval(refreshInterval);
  }
});

onMounted(() => {
  changeRefreshInterval(historyPeriod.value.refreshIntervalVal);
});
</script>

<template>
  <div class="container monitoring-view">
    <ServiceControlAvailable>
      <LicenseNotExpired>
        <!--MonitoringAvailable-->
        <div class="row">
          <div class="col-sm-12">
            <MonitoringNotAvailable v-if="monitoringConnectionState.unableToConnect || isMonitoringDisabled"></MonitoringNotAvailable>
          </div>
        </div>
        <!--Header-->
        <div class="monitoring-head">
          <div class="endpoint-title no-side-padding list-section">
            <h1 aria-label="endpoint-title" aria-level="1" class="righ-side-ellipsis" v-tippy="endpointName">
              {{ endpointName }}
            </h1>
            <div class="endpoint-status">
              <span role="status" aria-label="negative-critical-time-warning" class="warning" v-if="negativeCriticalTimeIsPresent">
                <i class="fa pa-warning" v-tippy="`Warning: endpoint currently has negative critical time, possibly because of a clock drift.`"></i>
              </span>
              <span role="status" aria-label="stale-warning" v-if="endpoint.isStale" class="warning">
                <i class="fa pa-endpoint-lost endpoint-details" v-tippy="`Unable to connect to endpoint`"></i>
              </span>
              <span role="status" aria-label="disconnected-warning" class="warning" v-if="endpoint.isScMonitoringDisconnected">
                <i class="fa pa-monitoring-lost endpoint-details" v-tippy="`Unable to connect to monitoring server`"></i>
              </span>
              <span role="status" aria-label="error-count-warning" class="warning" v-if="endpoint.errorCount" v-tippy="endpoint.errorCount + ` failed messages associated with this endpoint. Click to see list.`">
                <RouterLink :to="routeLinks.failedMessage.group.link(endpoint.serviceControlId)" v-if="endpoint.errorCount" class="warning cursorpointer">
                  <FAIcon :icon="faEnvelope" class="endpoint-status-icon" />
                  <span aria-label="error-count" class="badge badge-important ng-binding cursorpointer"> {{ endpoint.errorCount }}</span>
                </RouterLink>
              </span>
            </div>
          </div>
          <!--filters-->
          <div class="no-side-padding toolbar-menus">
            <div class="filter-monitoring">
              <PeriodSelector />
            </div>
          </div>
        </div>
        <!--large graphs-->
        <div role="grid" aria-label="detail-graphs-data" class="large-graphs">
          <div class="container">
            <div role="row" class="row">
              <EndpointBacklog v-model="endpoint" />
              <EndpointWorkload v-model="endpoint" />
              <EndpointTimings v-model="endpoint" />
            </div>
          </div>
        </div>

        <!--Messagetypes and instances-->
        <div>
          <!--tabs-->
          <div class="tabs">
            <h5 :class="{ active: activeTab === tabs.messageTypeBreakdown }">
              <a @click="activeTab = tabs.messageTypeBreakdown" class="cursorpointer ng-binding"
                >Message Types (<span aria-label="message-types-count">{{ endpoint.messageTypes.length }}</span
                >)</a
              >
            </h5>
            <h5 :class="{ active: activeTab === tabs.instancesBreakdown }">
              <a @click="activeTab = tabs.instancesBreakdown" class="cursorpointer ng-binding"
                >Instances (<span aria-label="instances-count">{{ endpoint.instances.length }}</span
                >)</a
              >
            </h5>
          </div>

          <!--showInstancesBreakdown-->
          <section v-if="activeTab === tabs.instancesBreakdown" class="endpoint-instances">
            <EndpointInstances />
          </section>

          <!--ShowMessagetypes breakdown-->
          <section v-if="activeTab === tabs.messageTypeBreakdown" class="endpoint-message-types">
            <EndpointMessageTypes />
          </section>
        </div>
      </LicenseNotExpired>
    </ServiceControlAvailable>
  </div>
</template>

<style scoped>
@import "../list.css";
@import "./monitoring.css";
@import "./endpoint.css";

.monitoring-head {
  display: flex;
  justify-content: space-between;
}

.monitoring-head h1 {
  margin-bottom: 10px;
  text-overflow: ellipsis;
  overflow: hidden;
  white-space: nowrap;
}

.monitoring-head .msg-group-menu {
  margin: 6px 0px 0 6px;
  padding-right: 0;
}

.monitoring-head .endpoint-status {
  top: 4px;
}

.monitoring-head .endpoint-status-icon {
  font-size: 26px;
  position: relative;
  left: 1px;
}

.monitoring-head .endpoint-status .badge {
  position: absolute;
  font-size: 10px;
  right: -10px;
  left: unset;
  top: unset;
  bottom: -2px;
}

.monitoring-head .endpoint-status .pa-endpoint-lost.endpoint-details,
.monitoring-head .endpoint-status .pa-monitoring-lost.endpoint-details {
  width: 32px;
  height: 30px;
}

.endpoint-title {
  flex: 0;
  display: flex;
  align-items: center;
}

.large-graphs {
  width: 100%;
  background-color: white;
  margin-bottom: 34px;
  padding: 30px 0;
}
</style>
