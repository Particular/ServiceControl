<script setup lang="ts">
import { computed } from "vue";
import { connectionState, environment, monitoringConnectionState, newVersions } from "../composables/serviceServiceControl";
import { monitoringUrl, serviceControlUrl } from "../composables/serviceServiceControlUrls";
import routeLinks from "@/router/routeLinks";

const isMonitoringEnabled = computed(() => {
  return monitoringUrl.value !== "!" && monitoringUrl.value !== "" && monitoringUrl.value !== null && monitoringUrl.value !== undefined;
});

const scAddressTooltip = computed(() => {
  return `ServiceControl URL ${serviceControlUrl.value}`;
});

const scMonitoringAddressTooltip = computed(() => {
  return `Monitoring URL ${monitoringUrl.value}`;
});
</script>

<template>
  <footer class="footer">
    <div class="container">
      <div class="row">
        <div class="connectivity-status">
          <span class="secondary">
            <i class="fa fa-plus sp-blue"></i>
            <RouterLink :to="routeLinks.configuration.endpointConnection.link">Connect new endpoint</RouterLink>
          </span>

          <span v-if="!newVersions.newSPVersion.newspversion && environment.sp_version"> ServicePulse v{{ environment.sp_version }} </span>
          <span v-if="newVersions.newSPVersion.newspversion && environment.sp_version">
            ServicePulse v{{ environment.sp_version }} (<i v-if="newVersions.newSPVersion.newspversionnumber" class="fa fa-level-up fake-link"></i>
            <a :href="newVersions.newSPVersion.newspversionlink" target="_blank">v{{ newVersions.newSPVersion.newspversionnumber }} available</a>)
          </span>
          <span :title="scAddressTooltip">
            Service Control:
            <span class="connected-status" v-if="connectionState.connected && !connectionState.connecting">
              <div class="fa pa-connection-success"></div>
              <span v-if="!environment.sc_version">Connected</span>
              <span v-if="environment.sc_version" class="versionnumber">v{{ environment.sc_version }}</span>
              <span v-if="newVersions.newSCVersion.newscversion" class="newscversion"
                >(<i class="fa fa-level-up fake-link"></i> <a :href="newVersions.newSCVersion.newscversionlink" target="_blank">v{{ newVersions.newSCVersion.newscversionnumber }} available</a>)</span
              >
            </span>
            <span v-if="!connectionState.connected && !connectionState.connecting" class="connection-failed"> <i class="fa pa-connection-failed"></i> Not connected </span>
            <span v-if="connectionState.connecting" class="connection-establishing"> <i class="fa pa-connection-establishing"></i> Connecting </span>
          </span>

          <template v-if="isMonitoringEnabled">
            <span class="monitoring-connected" :title="scMonitoringAddressTooltip">
              SC Monitoring:
              <span class="connected-status" v-if="monitoringConnectionState.connected && !monitoringConnectionState.connecting">
                <div class="fa pa-connection-success"></div>
                <span v-if="environment.monitoring_version"> v{{ environment.monitoring_version }}</span>
                <span v-if="newVersions.newMVersion.newmversion"
                  >(<i class="fa fa-level-up fake-link"></i> <a :href="newVersions.newMVersion.newmversionlink" target="_blank">v{{ newVersions.newMVersion.newmversionnumber }} available</a>)</span
                >
              </span>
              <span v-if="!monitoringConnectionState.connected && !monitoringConnectionState.connecting" class="connection-failed"> <i class="fa pa-connection-failed"></i> Not connected </span>
              <span v-if="monitoringConnectionState.connecting" class="connection-establishing"> <i class="fa pa-connection-establishing"></i> Connecting </span>
            </span>
          </template>
        </div>
      </div>
    </div>
  </footer>
</template>
