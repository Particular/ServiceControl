<script setup lang="ts">
import { onMounted, computed, watch } from "vue";
import { useRouter } from "vue-router";
import "bootstrap";
import { monitoringUrl, serviceControlUrl, useIsMonitoringDisabled } from "@/composables/serviceServiceControlUrls";
import "highlight.js/styles/github-dark.css";
import { useServiceControl, monitoringConnectionState, connectionState, environment } from "@/composables/serviceServiceControl";
import routeLinks from "@/router/routeLinks";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";

const router = useRouter();

onMounted(async () => {
  await useServiceControl();
});

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
  if (useIsMonitoringDisabled()) {
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
    console.debug("New Value: ", newValue);
    if (newValue === false) {
      useShowToast(TYPE.ERROR, "Error", `You are using Service Control version ${environment.sc_version}. Please, upgrade to version ${environment.minimum_supported_sc_version} or higher to unlock new functionality in ServicePulse.`);
    }
  }
);
</script>
<template>
  <div></div>
</template>
