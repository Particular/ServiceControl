<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
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
import { useLink, useRouter } from "vue-router";
import { useThroughputStore } from "@/stores/ThroughputStore";
import { storeToRefs } from "pinia";

const redirectCount = ref(0);
const { hasErrors } = storeToRefs(useThroughputStore());
watch(redirectCountUpdated, () => (redirectCount.value = redirectCountUpdated.count));

onMounted(async () => {
  if (notConnected.value) {
    const router = useRouter();

    if (router.currentRoute.value.name !== defaultRouteNotConnected.name) {
      await router.push({ path: defaultRouteNotConnected.path });
      return;
    }
  }

  const result = await useRedirects();
  redirectCount.value = result.total;
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
          <h5 :class="{ active: isRouteSelected(routeLinks.configuration.license.link), disabled: notConnected }" @click.capture="preventIfDisabled" class="nav-item">
            <RouterLink :to="routeLinks.configuration.license.link">License</RouterLink>
            <exclamation-mark :type="convertToWarningLevel(licenseStatus.warningLevel)" />
          </h5>
          <h5
            :class="{ active: isRouteSelected(routeLinks.throughput.setup.root) || isRouteSelected(routeLinks.throughput.setup.mask.link) || isRouteSelected(routeLinks.throughput.setup.diagnostics.link), disabled: notConnected }"
            @click.capture="preventIfDisabled"
            class="nav-item"
          >
            <RouterLink :to="routeLinks.throughput.setup.root">Usage Setup</RouterLink>
            <exclamation-mark :type="WarningLevel.Danger" v-if="hasErrors" />
          </h5>
          <template v-if="!licenseStatus.isExpired">
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.massTransitConnector.link), disabled: notConnected }" @click.capture="preventIfDisabled" class="nav-item">
              <RouterLink :to="routeLinks.configuration.massTransitConnector.link">MassTransit Connector</RouterLink>
            </h5>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.healthCheckNotifications.link), disabled: notConnected }" @click.capture="preventIfDisabled" class="nav-item">
              <RouterLink :to="routeLinks.configuration.healthCheckNotifications.link">Health Check Notifications</RouterLink>
            </h5>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.retryRedirects.link), disabled: notConnected }" @click.capture="preventIfDisabled" class="nav-item">
              <RouterLink :to="routeLinks.configuration.retryRedirects.link">Retry Redirects ({{ redirectCount }})</RouterLink>
            </h5>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.connections.link) }" class="nav-item">
              <RouterLink :to="routeLinks.configuration.connections.link">
                Connections
                <exclamation-mark v-if="displayConnectionsWarning" :type="WarningLevel.Danger" />
              </RouterLink>
            </h5>
            <h5 :class="{ active: isRouteSelected(routeLinks.configuration.endpointConnection.link), disabled: notConnected }" @click.capture="preventIfDisabled" class="nav-item">
              <RouterLink :to="routeLinks.configuration.endpointConnection.link">Endpoint Connection</RouterLink>
            </h5>
          </template>
        </div>
      </div>
    </div>
    <RouterView />
  </div>
</template>
