<script setup lang="ts">
import { computed, onMounted } from "vue";
import ExclamationMark from "../components/ExclamationMark.vue";
import convertToWarningLevel from "@/components/configuration/convertToWarningLevel";
import routeLinks from "@/router/routeLinks";
import isRouteSelected from "@/composables/isRouteSelected";
import { WarningLevel } from "@/components/WarningLevel";
import { useLink, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import useThroughputStoreAutoRefresh from "@/composables/useThroughputStoreAutoRefresh";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";
import { useRedirectsStore } from "@/stores/RedirectsStore";
import { useLicenseStore } from "@/stores/LicenseStore";

const { store: throughputStore } = useThroughputStoreAutoRefresh();
const { hasErrors } = storeToRefs(throughputStore);
const { store: connectionStore } = useConnectionsAndStatsAutoRefresh();
const connectionState = connectionStore.connectionState;
const redirectsStore = useRedirectsStore();
const licenseStore = useLicenseStore();
const { licenseStatus } = licenseStore;

onMounted(async () => {
  if (notConnected.value) {
    const router = useRouter();

    if (router.currentRoute.value.name !== defaultRouteNotConnected.name) {
      await router.push({ path: defaultRouteNotConnected.path });
      return;
    }
  }

  redirectsStore.refresh();
});

const notConnected = computed(() => !connectionState.connected && !connectionState.connectedRecently);

const defaultRouteNotConnected = useLink({ to: routeLinks.configuration.connections.link }).route.value;

function preventIfDisabled(e: Event) {
  if (notConnected.value) {
    e.preventDefault();
  }
}
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
          <h5 :class="{ active: isRouteSelected(routeLinks.configuration.license.link), disabled: notConnected }" @click.capture="preventIfDisabled" class="nav-item" role="tab" aria-label="license">
            <RouterLink :to="routeLinks.configuration.license.link">License</RouterLink>
            <exclamation-mark :type="convertToWarningLevel(licenseStatus.warningLevel)" />
          </h5>
          <h5
            :class="{ active: isRouteSelected(routeLinks.throughput.setup.root) || isRouteSelected(routeLinks.throughput.setup.mask.link) || isRouteSelected(routeLinks.throughput.setup.diagnostics.link), disabled: notConnected }"
            @click.capture="preventIfDisabled"
            class="nav-item"
            role="tab"
            aria-label="usage-setup"
          >
            <RouterLink :to="routeLinks.throughput.setup.root">Usage Setup</RouterLink>
            <exclamation-mark :type="WarningLevel.Danger" v-if="hasErrors" />
          </h5>
          <template v-if="!licenseStatus.isExpired">
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.massTransitConnector.link), disabled: notConnected }" @click.capture="preventIfDisabled" class="nav-item" role="tab" aria-label="mass-transit-connector">
              <RouterLink :to="routeLinks.configuration.massTransitConnector.link">MassTransit Connector</RouterLink>
            </h5>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.healthCheckNotifications.link), disabled: notConnected }" @click.capture="preventIfDisabled" class="nav-item" role="tab" aria-label="health-check-notifications">
              <RouterLink :to="routeLinks.configuration.healthCheckNotifications.link">Health Check Notifications</RouterLink>
            </h5>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.retryRedirects.link), disabled: notConnected }" @click.capture="preventIfDisabled" class="nav-item" role="tab" aria-label="retry-redirects">
              <RouterLink :to="routeLinks.configuration.retryRedirects.link">Retry Redirects ({{ redirectsStore.redirects.total }})</RouterLink>
            </h5>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.connections.link) }" class="nav-item" role="tab" aria-label="connections">
              <RouterLink :to="routeLinks.configuration.connections.link">
                Connections
                <exclamation-mark v-if="connectionStore.displayConnectionsWarning" :type="WarningLevel.Danger" />
              </RouterLink>
            </h5>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.endpointConnection.link), disabled: notConnected }" @click.capture="preventIfDisabled" class="nav-item" role="tab" aria-label="endpoint-connection">
              <RouterLink :to="routeLinks.configuration.endpointConnection.link">Endpoint Connection</RouterLink>
            </h5>
          </template>
          <template v-else>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.connections.link) }" class="nav-item" role="tab" aria-label="connections">
              <RouterLink :to="routeLinks.configuration.connections.link">
                Connections
                <exclamation-mark v-if="connectionStore.displayConnectionsWarning" :type="WarningLevel.Danger" />
              </RouterLink>
            </h5>
          </template>
        </div>
      </div>
    </div>
    <RouterView />
  </div>
</template>
