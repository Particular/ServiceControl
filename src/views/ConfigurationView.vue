<script setup lang="ts">
import { onMounted, ref, watch } from "vue";
import { licenseStatus } from "@/composables/serviceLicense";
import { connectionState } from "@/composables/serviceServiceControl";
import { useRedirects } from "@/composables/serviceRedirects";
import ExclamationMark from "../components/ExclamationMark.vue";
import convertToWarningLevel from "@/components/configuration/convertToWarningLevel";
import redirectCountUpdated from "@/components/configuration/redirectCountUpdated";
import routeLinks from "@/router/routeLinks";
import isRouteSelected from "@/composables/isRouteSelected";
import { WarningLevel } from "@/components/WarningLevel";
import { displayConnectionsWarning } from "@/components/configuration/displayConnectionsWarning";

const redirectCount = ref(0);

watch(redirectCountUpdated, () => (redirectCount.value = redirectCountUpdated.count));

onMounted(async () => {
  const result = await useRedirects();
  redirectCount.value = result.total;
});
</script>

<template>
  <div class="container">
    <div class="row">
      <div class="col-sm-12">
        <h1>Configuration</h1>
      </div>
    </div>
    <div class="row">
      <div class="col-sm-12">
        <div class="nav tabs">
          <h5 :class="{ active: isRouteSelected(routeLinks.configuration.license.link), disabled: !connectionState.connected && !connectionState.connectedRecently }" class="nav-item">
            <RouterLink :to="routeLinks.configuration.license.link">License</RouterLink>
            <exclamation-mark :type="convertToWarningLevel(licenseStatus.warningLevel)" />
          </h5>
          <template v-if="!licenseStatus.isExpired">
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.healthCheckNotifications.link), disabled: !connectionState.connected && !connectionState.connectedRecently }" class="nav-item">
              <RouterLink :to="routeLinks.configuration.healthCheckNotifications.link">Health Check Notifications</RouterLink>
            </h5>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.retryRedirects.link), disabled: !connectionState.connected && !connectionState.connectedRecently }" class="nav-item">
              <RouterLink :to="routeLinks.configuration.retryRedirects.link">Retry Redirects ({{ redirectCount }})</RouterLink>
            </h5>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.connections.link) }" class="nav-item">
              <RouterLink :to="routeLinks.configuration.connections.link">
                Connections
                <exclamation-mark v-if="displayConnectionsWarning" :type="WarningLevel.Danger" />
              </RouterLink>
            </h5>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.endpointConnection.link), disabled: !connectionState.connected && !connectionState.connectedRecently }" class="nav-item">
              <RouterLink :to="routeLinks.configuration.endpointConnection.link">Endpoint Connection</RouterLink>
            </h5>
          </template>
        </div>
      </div>
    </div>
    <RouterView />
  </div>
</template>
