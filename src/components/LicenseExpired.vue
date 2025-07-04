<script setup lang="ts">
import { licenseStatus, license } from "../composables/serviceLicense";
import routeLinks from "@/router/routeLinks";
import FAIcon from "@/components/FAIcon.vue";
import { faExternalLink } from "@fortawesome/free-solid-svg-icons";
</script>

<template>
  <template v-if="licenseStatus.isPlatformExpired">
    <div class="text-center monitoring-no-data">
      <h1>Platform license expired</h1>
      <p>Please update your license to continue using the Particular Service Platform</p>
      <div class="action-toolbar">
        <RouterLink class="btn btn-default btn-primary" :to="routeLinks.configuration.license.link">View license details</RouterLink>
      </div>
    </div>
  </template>
  <template v-if="licenseStatus.isPlatformTrialExpired">
    <div class="text-center monitoring-no-data">
      <h1>License expired</h1>
      <p>To continue using the Particular Service Platform, please extend your license</p>
      <div class="action-toolbar">
        <a class="btn btn-default btn-primary" :href="license.license_extension_url" target="_blank">Extend your license <FAIcon :icon="faExternalLink" /></a>
        <RouterLink class="btn btn-default btn-secondary" :to="routeLinks.configuration.license.link">View license details</RouterLink>
      </div>
    </div>
  </template>
  <template v-if="licenseStatus.isInvalidDueToUpgradeProtectionExpired">
    <div class="text-center monitoring-no-data">
      <h1>Platform license expired</h1>
      <p>Your upgrade protection period has elapsed and your license is not valid for this version of ServicePulse.</p>
      <div class="action-toolbar">
        <RouterLink class="btn btn-default btn-primary" :to="routeLinks.configuration.license.link">View license details</RouterLink>
      </div>
    </div>
  </template>
</template>
