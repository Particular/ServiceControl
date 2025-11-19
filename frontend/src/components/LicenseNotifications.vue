<script setup lang="ts">
import { watch, onMounted } from "vue";
import { LicenseStatus } from "@/resources/LicenseInfo";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import routeLinks from "@/router/routeLinks";
import { useRouter } from "vue-router";
import { useConfigurationStore } from "@/stores/ConfigurationStore";
import { storeToRefs } from "pinia";
import { useLicenseStore } from "@/stores/LicenseStore";

const router = useRouter();
const licenseStore = useLicenseStore();
const license = licenseStore.license;

const configurationStore = useConfigurationStore();
const { configuration } = storeToRefs(configurationStore);

function displayWarningMessage(licenseStatus: LicenseStatus) {
  const configurationRootLink = router.resolve(routeLinks.configuration.root).href;
  switch (licenseStatus) {
    case LicenseStatus.ValidWithExpiredUpgradeProtection: {
      const upgradeProtectionExpired = `<div><strong>Upgrade protection expired</strong><div>Once upgrade protection expires, you'll no longer have access to support or new product versions</div><a href="${configurationRootLink}" class="btn btn-warning">View license details</a></div>`;
      useShowToast(TYPE.WARNING, "", upgradeProtectionExpired, true);
      break;
    }
    case LicenseStatus.ValidWithExpiringTrial: {
      const trialExpiring = configuration.value?.mass_transit_connector
        ? `<div><strong>Early Access license expiring</strong><div>Your Early Access license will expire soon. To continue using the Particular Service Platform you'll need to extend your license.</div><a href="${license.license_extension_url}" class="btn btn-warning">Extend your license</a><a href="${configurationRootLink}" class="btn btn-light">View license details</a></div>`
        : `<div><strong>Non-production development license expiring</strong><div>Your non-production development license will expire soon. To continue using the Particular Service Platform you'll need to extend your license.</div><a href="${license.license_extension_url}" class="btn btn-warning">Extend your license</a><a href="${configurationRootLink}" class="btn btn-light">View license details</a></div>`;
      useShowToast(TYPE.WARNING, "", trialExpiring, true);
      break;
    }
    case LicenseStatus.ValidWithExpiringSubscription: {
      const subscriptionExpiring = `<div><strong>Platform license expires soon</strong><div>Once the license expires you'll no longer be able to continue using the Particular Service Platform.</div><a href="${configurationRootLink}" class="btn btn-warning">View license details</a></div>`;
      useShowToast(TYPE.WARNING, "", subscriptionExpiring, true);
      break;
    }
    case LicenseStatus.ValidWithExpiringUpgradeProtection: {
      const upgradeProtectionExpiring = `<div><strong>Upgrade protection expires soon</strong><div>Once upgrade protection expires, you'll no longer have access to support or new product versions</div><a href="${configurationRootLink}" class="btn btn-warning">View license details</a></div>`;
      useShowToast(TYPE.WARNING, "", upgradeProtectionExpiring, true);
      break;
    }
    case LicenseStatus.InvalidDueToExpiredTrial:
    case LicenseStatus.InvalidDueToExpiredSubscription:
    case LicenseStatus.InvalidDueToExpiredUpgradeProtection:
      useShowToast(TYPE.ERROR, "Error", 'Your license has expired. Please contact Particular Software support at: <a href="http://particular.net/support">http://particular.net/support</a>', true);
      break;
  }
}

watch(
  () => license.license_status,
  (newValue, oldValue) => {
    const checkForWarnings = newValue !== oldValue;
    if (checkForWarnings) {
      displayWarningMessage(newValue);
    }
  }
);

onMounted(async () => {
  await licenseStore.refresh();
});
</script>
<template>
  <template></template>
</template>
