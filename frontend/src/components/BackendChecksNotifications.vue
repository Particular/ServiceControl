<script setup lang="ts">
import { computed, watch } from "vue";
import { useRouter } from "vue-router";
import "bootstrap";
import routeLinks from "@/router/routeLinks";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";
import useEnvironmentAndVersionsAutoRefresh from "@/composables/useEnvironmentAndVersionsAutoRefresh";
import { useServiceControlStore } from "@/stores/ServiceControlStore";
import { storeToRefs } from "pinia";

const router = useRouter();

const { store: connectionStore } = useConnectionsAndStatsAutoRefresh();
const connectionState = connectionStore.connectionState;
const monitoringConnectionState = connectionStore.monitoringConnectionState;
const { store: environmentStore } = useEnvironmentAndVersionsAutoRefresh();
const environment = environmentStore.environment;
const serviceControlStore = useServiceControlStore();
const { monitoringUrl, serviceControlUrl, isMonitoringDisabled } = storeToRefs(serviceControlStore);

const primaryConnectionFailure = computed(() => connectionState.unableToConnect);
const monitoringConnectionFailure = computed(() => monitoringConnectionState.unableToConnect);

watch(primaryConnectionFailure, (newValue, oldValue) => {
  //NOTE to eliminate success msg showing everytime the screen is refreshed
  if (newValue !== oldValue && !(oldValue === null && newValue === false)) {
    const connectionUrl = router.resolve(routeLinks.configuration.connections.link).href;
    if (newValue) {
      useShowToast(TYPE.ERROR, "Error", `Could not connect to ServiceControl at ${serviceControlUrl.value}. <a class="btn btn-default" href="${connectionUrl}">View connection settings</a>`);
    } else {
      useShowToast(TYPE.SUCCESS, "Success", `Connection to ServiceControl was successful at ${serviceControlUrl.value}.`);
    }
  }
});

watch(monitoringConnectionFailure, (newValue, oldValue) => {
  // Only watch the state change if monitoring is enabled
  if (isMonitoringDisabled.value) {
    return;
  }

  //NOTE to eliminate success msg showing everytime the screen is refreshed
  if (newValue !== oldValue && !(oldValue === null && newValue === false)) {
    const connectionUrl = router.resolve(routeLinks.configuration.connections.link).href;
    if (newValue) {
      useShowToast(TYPE.ERROR, "Error", `Could not connect to the ServiceControl Monitoring service at ${monitoringUrl.value}. <a class="btn btn-default" href="${connectionUrl}">View connection settings</a>`);
    } else {
      useShowToast(TYPE.SUCCESS, "Success", `Connection to ServiceControl Monitoring service was successful at ${monitoringUrl.value}.`);
    }
  }
});

watch(
  () => environment.is_compatible_with_sc,
  (newValue) => {
    if (newValue === false) {
      useShowToast(TYPE.ERROR, "Error", `You are using Service Control version ${environment.sc_version}. Please, upgrade to version ${environment.minimum_supported_sc_version} or higher to unlock new functionality in ServicePulse.`);
    }
  }
);
</script>
<template>
  <template></template>
</template>
