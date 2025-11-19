import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, reactive, ref } from "vue";
import { useServiceControlStore } from "./ServiceControlStore";
import LicenseInfo, { LicenseStatus } from "@/resources/LicenseInfo";
import { LicenseWarningLevel } from "@/composables/LicenseStatus";
import { useGetDayDiffFromToday } from "@/composables/formatter";

export const useLicenseStore = defineStore("LicenseStore", () => {
  const serviceControlStore = useServiceControlStore();

  const license = reactive<LicenseInfo>({
    edition: "",
    expiration_date: "",
    upgrade_protection_expiration: "",
    license_type: "",
    instance_name: "",
    trial_license: true,
    registered_to: "",
    status: "",
    license_status: LicenseStatus.Unavailable,
    license_extension_url: "",
  });

  const licenseStatus = reactive({
    isSubscriptionLicense: false,
    isUpgradeProtectionLicense: false,
    isTrialLicense: false,
    isPlatformExpired: false,
    isPlatformTrialExpired: false,
    isPlatformTrialExpiring: false,
    isInvalidDueToUpgradeProtectionExpired: false,
    isValidWithExpiredUpgradeProtection: false,
    isValidWithExpiringUpgradeProtection: false,
    isExpired: false,
    upgradeDaysLeft: "",
    subscriptionDaysLeft: "",
    trialDaysLeft: "",
    warningLevel: LicenseWarningLevel.None,
    licenseExtensionUrl: "",
  });

  const loading = ref(false);

  // Computed properties for license formatting
  const licenseEdition = computed(() => {
    return `${license.license_type}${license.edition ? `, ${license.edition}` : ""}`;
  });

  const formattedInstanceName = computed(() => {
    return license.instance_name || "Upgrade ServiceControl to v3.4.0+ to see more information about this license";
  });

  const formattedExpirationDate = computed(() => {
    return license.expiration_date ? new Date(license.expiration_date.replace("Z", "")).toLocaleDateString() : "";
  });

  const formattedUpgradeProtectionExpiration = computed(() => {
    return license.upgrade_protection_expiration ? new Date(license.upgrade_protection_expiration.replace("Z", "")).toLocaleDateString() : "";
  });

  async function refresh() {
    loading.value = true;
    try {
      const lic = await getLicense();
      if (lic === null) {
        return;
      }
      license.license_type = lic.license_type;
      license.expiration_date = lic.expiration_date;
      license.trial_license = lic.trial_license;
      license.edition = lic.edition;
      license.license_status = lic.license_status;
      license.instance_name = lic.instance_name;
      license.registered_to = lic.registered_to;
      license.status = lic.status;
      license.license_extension_url = lic.license_extension_url ?? "https://particular.net/extend-your-trial?p=servicepulse";
      license.upgrade_protection_expiration = lic.upgrade_protection_expiration;

      licenseStatus.isSubscriptionLicense = isSubscriptionLicense();
      licenseStatus.isUpgradeProtectionLicense = isUpgradeProtectionLicense();
      licenseStatus.isTrialLicense = license.trial_license;
      licenseStatus.isPlatformExpired = license.license_status === LicenseStatus.InvalidDueToExpiredSubscription;
      licenseStatus.isPlatformTrialExpiring = license.license_status === LicenseStatus.ValidWithExpiringTrial;
      licenseStatus.isPlatformTrialExpired = license.license_status === LicenseStatus.InvalidDueToExpiredTrial;
      licenseStatus.isInvalidDueToUpgradeProtectionExpired = license.license_status === LicenseStatus.InvalidDueToExpiredUpgradeProtection;
      licenseStatus.isValidWithExpiredUpgradeProtection = license.license_status === LicenseStatus.ValidWithExpiredUpgradeProtection;
      licenseStatus.isValidWithExpiringUpgradeProtection = license.license_status === LicenseStatus.ValidWithExpiringUpgradeProtection;
      licenseStatus.upgradeDaysLeft = getUpgradeDaysLeft();
      licenseStatus.subscriptionDaysLeft = getSubscriptionDaysLeft();
      licenseStatus.trialDaysLeft = getTrialDaysLeft();
      licenseStatus.warningLevel = getLicenseWarningLevel();
      licenseStatus.isExpired = licenseStatus.isPlatformExpired || licenseStatus.isPlatformTrialExpired || licenseStatus.isInvalidDueToUpgradeProtectionExpired;
      licenseStatus.licenseExtensionUrl = license.license_extension_url;
    } finally {
      loading.value = false;
    }
  }

  async function getLicense() {
    try {
      const [, data] = await serviceControlStore.fetchTypedFromServiceControl<LicenseInfo>("license?refresh=true&clientName=servicepulse");
      return data;
    } catch (err) {
      console.error("Error fetching license information", err);
      return null;
    }
  }

  function getLicenseWarningLevel() {
    switch (license.license_status) {
      case LicenseStatus.InvalidDueToExpiredTrial:
      case LicenseStatus.InvalidDueToExpiredSubscription:
      case LicenseStatus.InvalidDueToExpiredUpgradeProtection:
        return LicenseWarningLevel.Danger;
      case LicenseStatus.ValidWithExpiringUpgradeProtection:
      case LicenseStatus.ValidWithExpiringTrial:
      case LicenseStatus.ValidWithExpiredUpgradeProtection:
      case LicenseStatus.ValidWithExpiringSubscription:
        return LicenseWarningLevel.Warning;
      default:
        return LicenseWarningLevel.None;
    }
  }

  function isUpgradeProtectionLicense() {
    return license.upgrade_protection_expiration !== undefined && license.upgrade_protection_expiration !== "";
  }

  function isSubscriptionLicense() {
    return license.expiration_date !== undefined && license.expiration_date !== "" && !license.trial_license;
  }

  function getSubscriptionDaysLeft() {
    if (license.license_status === LicenseStatus.InvalidDueToExpiredSubscription) return " - expired";

    const isExpiring = license.license_status === LicenseStatus.ValidWithExpiringSubscription;
    return getExpiringText(isExpiring, license.expiration_date);
  }

  function getTrialDaysLeft() {
    if (license.license_status === LicenseStatus.InvalidDueToExpiredTrial) return " - expired";

    const isExpiring = license.license_status === LicenseStatus.ValidWithExpiringTrial;
    return getExpiringText(isExpiring, license.expiration_date);
  }

  function getExpiringText(isExpiring: boolean, expirationDate: string) {
    const expiringIn = useGetDayDiffFromToday(expirationDate);
    if (isNaN(expiringIn)) return "";
    if (!isExpiring) return ` - ${expiringIn} days left`;
    if (expiringIn === 0) return " - expiring today";
    if (expiringIn === 1) return " - expiring tomorrow";
    return ` - expiring in ${expiringIn} days`;
  }

  function getUpgradeDaysLeft() {
    if (license.license_status === LicenseStatus.InvalidDueToExpiredUpgradeProtection) return " - expired";

    const expiringIn = useGetDayDiffFromToday(license.upgrade_protection_expiration);
    //TODO: can this be unified with the function above? Text is currently similar but not identical.
    if (isNaN(expiringIn)) return "";
    if (expiringIn <= 0) return " - expired";
    if (expiringIn === 0) return " - expiring today";
    if (expiringIn === 1) return " - 1 day left";
    return " - " + expiringIn + " days left";
  }

  return {
    refresh,
    license,
    licenseStatus,
    loading,
    licenseEdition,
    formattedInstanceName,
    formattedExpirationDate,
    formattedUpgradeProtectionExpiration,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useLicenseStore, import.meta.hot));
}

export type LicenseStore = ReturnType<typeof useLicenseStore>;
