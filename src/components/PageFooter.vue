<script setup lang="ts">
import { computed } from "vue";
import { connectionState, environment, monitoringConnectionState, newVersions } from "../composables/serviceServiceControl";
import { monitoringUrl, serviceControlUrl } from "../composables/serviceServiceControlUrls";
import { license, licenseStatus } from "../composables/serviceLicense";
import { LicenseStatus } from "@/resources/LicenseInfo";
import routeLinks from "@/router/routeLinks";
import { useConfiguration } from "@/composables/configuration";
import FAIcon from "@/components/FAIcon.vue";
import { faArrowTurnUp, faPlus } from "@fortawesome/free-solid-svg-icons";

const isMonitoringEnabled = computed(() => {
  return monitoringUrl.value !== "!" && monitoringUrl.value !== "" && monitoringUrl.value !== null && monitoringUrl.value !== undefined;
});

const scAddressTooltip = computed(() => {
  return `ServiceControl URL ${serviceControlUrl.value}`;
});

const scMonitoringAddressTooltip = computed(() => {
  return `Monitoring URL ${monitoringUrl.value}`;
});

const configuration = useConfiguration();
</script>

<template>
  <footer class="footer">
    <div class="container">
      <div class="row">
        <div class="connectivity-status">
          <span class="secondary">
            <FAIcon class="footer-icon" :icon="faPlus" />
            <RouterLink :to="routeLinks.configuration.endpointConnection.link">Connect new endpoint</RouterLink>
          </span>

          <span v-if="!newVersions.newSPVersion.newspversion && environment.sp_version"> ServicePulse v{{ environment.sp_version }} </span>
          <span v-if="newVersions.newSPVersion.newspversion && environment.sp_version">
            ServicePulse v{{ environment.sp_version }} (<FAIcon v-if="newVersions.newSPVersion.newspversionnumber" class="footer-icon fake-link" :icon="faArrowTurnUp" />
            <a :href="newVersions.newSPVersion.newspversionlink" target="_blank">v{{ newVersions.newSPVersion.newspversionnumber }} available</a>)
          </span>
          <span :title="scAddressTooltip">
            Service Control:
            <span class="connected-status" v-if="connectionState.connected && !connectionState.connecting">
              <div class="fa pa-connection-success"></div>
              <span v-if="!environment.sc_version">Connected</span>
              <span v-if="environment.sc_version" class="versionnumber">v{{ environment.sc_version }}</span>
              <span v-if="newVersions.newSCVersion.newscversion" class="newscversion"
                >(<FAIcon class="footer-icon fake-link" :icon="faArrowTurnUp" /> <a :href="newVersions.newSCVersion.newscversionlink" target="_blank">v{{ newVersions.newSCVersion.newscversionnumber }} available</a>)</span
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
                  >(<FAIcon class="footer-icon fake-link" :icon="faArrowTurnUp" /> <a :href="newVersions.newMVersion.newmversionlink" target="_blank">v{{ newVersions.newMVersion.newmversionnumber }} available</a>)</span
                >
              </span>
              <span v-if="!monitoringConnectionState.connected && !monitoringConnectionState.connecting" class="connection-failed"> <i class="fa pa-connection-failed"></i> Not connected </span>
              <span v-if="monitoringConnectionState.connecting" class="connection-establishing"> <i class="fa pa-connection-establishing"></i> Connecting </span>
            </span>
          </template>
        </div>
      </div>
      <template v-if="license.license_status !== LicenseStatus.Unavailable && !configuration?.mass_transit_connector && licenseStatus.isTrialLicense">
        <div class="row trialLicenseBar">
          <div role="status" aria-label="trial license bar information">
            <RouterLink :to="routeLinks.configuration.license.link">{{ license.license_type }} license</RouterLink>, non-production use only
          </div>
        </div>
      </template>
    </div>
  </footer>
</template>

<style scoped>
.footer-icon {
  color: var(--sp-blue);
  margin-right: 4px;
}
</style>
