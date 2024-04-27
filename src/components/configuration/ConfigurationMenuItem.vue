<script setup lang="ts">
import { RouterLink } from "vue-router";
import { computed } from "vue";
import { connectionState, monitoringConnectionState } from "@/composables/serviceServiceControl";
import { useIsMonitoringEnabled } from "@/composables/serviceServiceControlUrls";
import { licenseStatus } from "@/composables/serviceLicense";
import ExclamationMark from "@/components/ExclamationMark.vue";
import { LicenseWarningLevel } from "@/composables/LicenseStatus";
import { WarningLevel } from "@/components/WarningLevel";
import routeLinks from "@/router/routeLinks";

const displayWarn = computed(() => {
  return licenseStatus.warningLevel === LicenseWarningLevel.Warning;
});
const displayDanger = computed(() => {
  return connectionState.unableToConnect || (monitoringConnectionState.unableToConnect && useIsMonitoringEnabled()) || licenseStatus.warningLevel === LicenseWarningLevel.Danger;
});
</script>

<template>
  <RouterLink :to="routeLinks.configuration.root" exact>
    <i class="fa fa-cog icon-white" title="Configuration"></i>
    <span class="navbar-label">Configuration</span>
    <exclamation-mark :type="WarningLevel.Warning" v-if="displayWarn" />
    <exclamation-mark :type="WarningLevel.Danger" v-if="displayDanger" />
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
