<script setup lang="ts">
import { RouterLink } from "vue-router";
import { computed } from "vue";
import { licenseStatus } from "@/composables/serviceLicense";
import ExclamationMark from "@/components/ExclamationMark.vue";
import { LicenseWarningLevel } from "@/composables/LicenseStatus";
import { WarningLevel } from "@/components/WarningLevel";
import routeLinks from "@/router/routeLinks";
import { displayConnectionsWarning } from "@/components/configuration/displayConnectionsWarning";
import { useThroughputStore } from "@/stores/ThroughputStore";
import { storeToRefs } from "pinia";
import FAIcon from "@/components/FAIcon.vue";
import { faGear } from "@fortawesome/free-solid-svg-icons";

const { hasErrors } = storeToRefs(useThroughputStore());

const displayWarn = computed(() => {
  return licenseStatus.warningLevel === LicenseWarningLevel.Warning;
});
const displayDanger = computed(() => {
  return hasErrors.value || displayConnectionsWarning.value || licenseStatus.warningLevel === LicenseWarningLevel.Danger;
});
</script>

<template>
  <RouterLink :to="routeLinks.configuration.root" exact>
    <FAIcon :icon="faGear" v-tippy="'Configuration'" />
    <span class="navbar-label">Configuration</span>
    <exclamation-mark :type="WarningLevel.Danger" v-if="displayDanger" />
    <exclamation-mark :type="WarningLevel.Warning" v-else-if="displayWarn" />
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
