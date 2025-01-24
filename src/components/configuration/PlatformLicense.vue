<script setup lang="ts">
import { computed } from "vue";
import { license, licenseStatus } from "@/composables/serviceLicense";
import ServiceControlNotAvailable from "../ServiceControlNotAvailable.vue";
import { connectionState } from "@/composables/serviceServiceControl";
import BusyIndicator from "../BusyIndicator.vue";
import ExclamationMark from "./../../components/ExclamationMark.vue";
import convertToWarningLevel from "@/components/configuration/convertToWarningLevel";
import { useConfiguration } from "@/composables/configuration";
import { typeText } from "@/resources/LicenseInfo";

// This is needed because the ConfigurationView.vue routerView expects this event.
// The event is only actually emitted on the RetryRedirects.vue component
// but if we don't include it, the console will show warnings about not being able to
// subscribe to this event
defineEmits<{
  redirectCountUpdated: [count: number];
}>();

const loading = computed(() => {
  return !license;
});

const configuration = useConfiguration();
</script>

<template>
  <section name="license">
    <ServiceControlNotAvailable />
    <template v-if="!connectionState.unableToConnect">
      <section>
        <busy-indicator v-if="loading"></busy-indicator>

        <template v-if="!loading">
          <div class="box">
            <div class="row">
              <div class="license-info">
                <div><b>Platform license type:</b> {{ typeText(license, configuration) }}{{ license.licenseEdition }}</div>

                <template v-if="licenseStatus.isSubscriptionLicense">
                  <div>
                    <b>License expiry date: </b>
                    <span
                      :class="{
                        'license-expired': licenseStatus.isPlatformExpired,
                      }"
                    >
                      {{ license.formattedExpirationDate }}
                      {{ licenseStatus.subscriptionDaysLeft }}
                      <exclamation-mark :type="convertToWarningLevel(licenseStatus.warningLevel)" />
                    </span>
                    <div class="license-expired-text" v-if="licenseStatus.isPlatformExpired">Your license expired. Please update the license to continue using the Particular Service Platform.</div>
                  </div>
                </template>
                <template v-if="licenseStatus.isTrialLicense">
                  <div>
                    <b>License expiry date: </b>
                    <span
                      :class="{
                        'license-expired': licenseStatus.isPlatformTrialExpired,
                      }"
                    >
                      {{ license.formattedExpirationDate }}
                      {{ licenseStatus.trialDaysLeft }}
                      <exclamation-mark :type="convertToWarningLevel(licenseStatus.warningLevel)" />
                    </span>
                    <div class="license-expired-text" v-if="licenseStatus.isPlatformTrialExpired">Your license expired. To continue using the Particular Service Platform you'll need to extend your license.</div>
                    <div class="license-page-extend-trial" v-if="licenseStatus.isPlatformTrialExpiring && licenseStatus.isPlatformTrialExpired">
                      <a class="btn btn-default btn-primary" :href="license.license_extension_url" target="_blank">Extend your license&nbsp;&nbsp;<i class="fa fa-external-link"></i></a>
                    </div>
                  </div>
                </template>
                <template v-if="licenseStatus.isUpgradeProtectionLicense">
                  <div>
                    <span>
                      <b>Upgrade protection expiry date:</b>
                      <span
                        :class="{
                          'license-expired': licenseStatus.isInvalidDueToUpgradeProtectionExpired,
                        }"
                      >
                        {{ license.formattedUpgradeProtectionExpiration }}
                        {{ licenseStatus.upgradeDaysLeft }}
                        <exclamation-mark :type="convertToWarningLevel(licenseStatus.warningLevel)" />
                      </span>
                    </span>
                    <div class="license-expired-text" v-if="licenseStatus.isValidWithExpiredUpgradeProtection || licenseStatus.isValidWithExpiringUpgradeProtection">
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
                  <a href="https://particular.net/contactus">Contact us</a>
                  <i class="fa fa-external-link fake-link"></i>
                </div>
              </div>
            </div>
          </div>
        </template>
      </section>
    </template>
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
