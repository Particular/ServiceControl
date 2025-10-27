<script setup lang="ts">
import { computed } from "vue";
import ServiceControlAvailable from "../ServiceControlAvailable.vue";
import ExclamationMark from "./../../components/ExclamationMark.vue";
import convertToWarningLevel from "@/components/configuration/convertToWarningLevel";
import { typeText } from "@/resources/LicenseInfo";
import { faExternalLink } from "@fortawesome/free-solid-svg-icons";
import FAIcon from "@/components/FAIcon.vue";
import { useConfigurationStore } from "@/stores/ConfigurationStore";
import { storeToRefs } from "pinia";
import { useLicenseStore } from "@/stores/LicenseStore";
import LoadingSpinner from "../LoadingSpinner.vue";

const configurationStore = useConfigurationStore();
const { configuration } = storeToRefs(configurationStore);
const licenseStore = useLicenseStore();
const { licenseStatus, license } = licenseStore;

const loading = computed(() => {
  return !license || license.status === "";
});
</script>

<template>
  <section name="license">
    <ServiceControlAvailable>
      <section>
        <LoadingSpinner v-if="loading" />

        <template v-if="!loading">
          <div class="box">
            <div class="row">
              <div class="license-info">
                <div>
                  <b>Platform license type:</b> <span role="note" aria-label="license-type">{{ typeText(license, configuration) }}{{ license.licenseEdition }}</span>
                </div>

                <template v-if="licenseStatus.isSubscriptionLicense">
                  <div>
                    <b>License expiry date: </b>
                    <span
                      role="note"
                      aria-label="license-expiry-date"
                      :class="{
                        'license-expired': licenseStatus.isPlatformExpired,
                      }"
                    >
                      {{ license.formattedExpirationDate }}
                      <span role="note" aria-label="license-days-left">{{ licenseStatus.subscriptionDaysLeft }}</span>
                      <exclamation-mark :type="convertToWarningLevel(licenseStatus.warningLevel)" />
                    </span>
                    <div class="license-expired-text" role="note" aria-label="license-expired" v-if="licenseStatus.isPlatformExpired">Your license expired. Please update the license to continue using the Particular Service Platform.</div>
                  </div>
                </template>
                <template v-if="licenseStatus.isTrialLicense">
                  <div>
                    <b>License expiry date: </b>
                    <span
                      role="note"
                      aria-label="license-expiry-date"
                      :class="{
                        'license-expired': licenseStatus.isPlatformTrialExpired,
                      }"
                    >
                      {{ license.formattedExpirationDate }}
                      <span role="note" aria-label="license-days-left"> {{ licenseStatus.trialDaysLeft }}</span>
                      <exclamation-mark :type="convertToWarningLevel(licenseStatus.warningLevel)" />
                    </span>
                    <div class="license-expired-text" role="note" aria-label="license-expired" v-if="licenseStatus.isPlatformTrialExpired">
                      Your license expired. To continue using the Particular Service Platform you'll need to extend your license.
                    </div>
                    <div class="license-page-extend-trial" v-if="licenseStatus.isPlatformTrialExpiring && licenseStatus.isPlatformTrialExpired">
                      <a class="btn btn-default btn-primary" :href="license.license_extension_url" target="_blank">Extend your license <FAIcon :icon="faExternalLink" /></a>
                    </div>
                  </div>
                </template>
                <template v-if="licenseStatus.isUpgradeProtectionLicense">
                  <div>
                    <span>
                      <b>Upgrade protection expiry date:</b>
                      <span
                        role="note"
                        aria-label="license-expiry-date"
                        :class="{
                          'license-expired': licenseStatus.isInvalidDueToUpgradeProtectionExpired,
                        }"
                      >
                        {{ license.formattedUpgradeProtectionExpiration }}
                        <span role="note" aria-label="license-days-left">{{ licenseStatus.upgradeDaysLeft }}</span>
                        <exclamation-mark :type="convertToWarningLevel(licenseStatus.warningLevel)" />
                      </span>
                    </span>
                    <div class="license-expired-text" role="note" aria-label="license-expired" v-if="licenseStatus.isValidWithExpiredUpgradeProtection || licenseStatus.isValidWithExpiringUpgradeProtection">
                      <b>Warning:</b> Once upgrade protection expires, you'll no longer have access to support or new product versions.
                    </div>
                    <div class="license-expired-text" v-if="licenseStatus.isInvalidDueToUpgradeProtectionExpired">Your license upgrade protection expired before this version of ServicePulse was released.</div>
                  </div>
                </template>
                <div>
                  <b>ServiceControl instance:</b>
                  {{ license.formattedInstanceName }}
                </div>
                <ul class="license-install-info">
                  <li>
                    <a href="https://docs.particular.net/servicecontrol/license" target="_blank">Install or update a ServiceControl license</a>
                  </li>
                </ul>

                <div class="need-help">
                  Need help?
                  <a href="https://particular.net/contactus">Contact us <FAIcon :icon="faExternalLink" /></a>
                </div>
              </div>
            </div>
          </div>
        </template>
      </section>
    </ServiceControlAvailable>
  </section>
</template>

<style scoped>
.license-info {
  font-size: 16px;
  padding: 2em;
  line-height: 3em;
}

.license-install-info li {
  line-height: 1em;
}

.need-help {
  margin-top: 38px;
  padding-top: 20px;
  border-top: 2px solid #f2f2f2;
}
</style>
