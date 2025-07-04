import { computed, type ComputedRef, reactive, type UnwrapNestedRefs } from "vue";
import { useGetDayDiffFromToday } from "./formatter";
import { useTypedFetchFromServiceControl } from "./serviceServiceControlUrls";
import type LicenseInfo from "@/resources/LicenseInfo";
// eslint-disable-next-line no-duplicate-imports
import { LicenseStatus } from "@/resources/LicenseInfo";
import { LicenseWarningLevel } from "@/composables/LicenseStatus";

interface License extends LicenseInfo {
  licenseEdition: ComputedRef<string>;
  formattedExpirationDate: ComputedRef<string>;
  formattedUpgradeProtectionExpiration: ComputedRef<string>;
  formattedInstanceName: ComputedRef<string>;
}

class emptyLicense implements License {
  edition = "";
  expiration_date = "";
  upgrade_protection_expiration = "";
  license_type = "";
  instance_name = "";
  trial_license = true;
  registered_to = "";
  status = "";
  license_status = LicenseStatus.Unavailable;
  license_extension_url = "";
  licenseEdition = computed(() => {
    return `${this.license_type}${this.edition ? `, ${this.edition}` : ""}`;
  });
  formattedInstanceName = computed(() => {
    return this.instance_name || "Upgrade ServiceControl to v3.4.0+ to see more information about this license";
  });
  formattedExpirationDate = computed(() => {
    return this.expiration_date ? new Date(this.expiration_date.replace("Z", "")).toLocaleDateString() : "";
  });
  formattedUpgradeProtectionExpiration = computed(() => {
    return this.upgrade_protection_expiration ? new Date(this.upgrade_protection_expiration.replace("Z", "")).toLocaleDateString() : "";
  });
}

const license = reactive<License>(new emptyLicense());

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

function useLicense() {
  return { getOrUpdateLicenseStatus, license, licenseStatus };
}

async function getOrUpdateLicenseStatus() {
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

  licenseStatus.isSubscriptionLicense = isSubscriptionLicense(license);
  licenseStatus.isUpgradeProtectionLicense = isUpgradeProtectionLicense(license);
  licenseStatus.isTrialLicense = license.trial_license;
  licenseStatus.isPlatformExpired = license.license_status === LicenseStatus.InvalidDueToExpiredSubscription;
  licenseStatus.isPlatformTrialExpiring = license.license_status === LicenseStatus.ValidWithExpiringTrial;
  licenseStatus.isPlatformTrialExpired = license.license_status === LicenseStatus.InvalidDueToExpiredTrial;
  licenseStatus.isInvalidDueToUpgradeProtectionExpired = license.license_status === LicenseStatus.InvalidDueToExpiredUpgradeProtection;
  licenseStatus.isValidWithExpiredUpgradeProtection = license.license_status === LicenseStatus.ValidWithExpiredUpgradeProtection;
  licenseStatus.isValidWithExpiringUpgradeProtection = license.license_status === LicenseStatus.ValidWithExpiringUpgradeProtection;
  licenseStatus.upgradeDaysLeft = getUpgradeDaysLeft(license);
  licenseStatus.subscriptionDaysLeft = getSubscriptionDaysLeft(license);
  licenseStatus.trialDaysLeft = getTrialDaysLeft(license);
  licenseStatus.warningLevel = getLicenseWarningLevel(license.license_status);
  licenseStatus.isExpired = licenseStatus.isPlatformExpired || licenseStatus.isPlatformTrialExpired || licenseStatus.isInvalidDueToUpgradeProtectionExpired;
  licenseStatus.licenseExtensionUrl = license.license_extension_url;
}

export { useLicense, license, licenseStatus };

function getLicenseWarningLevel(licenseStatus: LicenseStatus) {
  if (licenseStatus === "InvalidDueToExpiredTrial" || licenseStatus === "InvalidDueToExpiredSubscription" || licenseStatus === "InvalidDueToExpiredUpgradeProtection") return LicenseWarningLevel.Danger;
  else if (licenseStatus === "ValidWithExpiringUpgradeProtection" || licenseStatus === "ValidWithExpiringTrial" || licenseStatus === "ValidWithExpiredUpgradeProtection" || licenseStatus === "ValidWithExpiringSubscription")
    return LicenseWarningLevel.Warning;
  return LicenseWarningLevel.None;
}

function isUpgradeProtectionLicense(license: UnwrapNestedRefs<License>) {
  return license.upgrade_protection_expiration !== undefined && license.upgrade_protection_expiration !== "";
}

function isSubscriptionLicense(license: UnwrapNestedRefs<License>) {
  return license.expiration_date !== undefined && license.expiration_date !== "" && !license.trial_license;
}

function getSubscriptionDaysLeft(license: UnwrapNestedRefs<License>) {
  if (license.license_status === "InvalidDueToExpiredSubscription") return " - expired";

  const isExpiring = license.license_status === "ValidWithExpiringSubscription";

  const expiringIn = useGetDayDiffFromToday(license.expiration_date);
  if (isNaN(expiringIn)) return "";
  if (!isExpiring) return " - " + expiringIn + " days left";
  if (expiringIn === 0) return " - expiring today";
  if (expiringIn === 1) return " - expiring tomorrow";
  return " - expiring in " + expiringIn + " days";
}

function getTrialDaysLeft(license: UnwrapNestedRefs<License>) {
  if (license.license_status === "InvalidDueToExpiredTrial") return " - expired";

  const isExpiring = license.license_status === "ValidWithExpiringTrial";

  const expiringIn = useGetDayDiffFromToday(license.expiration_date);
  if (isNaN(expiringIn)) return "";
  if (!isExpiring) return " - " + expiringIn + " days left";
  if (expiringIn === 0) return " - expiring today";
  if (expiringIn === 1) return " - expiring tomorrow";
  return " - expiring in " + expiringIn + " days";
}

function getUpgradeDaysLeft(license: UnwrapNestedRefs<License>) {
  if (license.license_status === "InvalidDueToExpiredUpgradeProtection") return " - expired";

  const expiringIn = useGetDayDiffFromToday(license.upgrade_protection_expiration);
  if (isNaN(expiringIn)) return "";
  if (expiringIn <= 0) return " - expired";
  if (expiringIn === 0) return " - expiring today";
  if (expiringIn === 1) return " - 1 day left";
  return " - " + expiringIn + " days left";
}

async function getLicense() {
  try {
    const [, data] = await useTypedFetchFromServiceControl<LicenseInfo>("license?refresh=true&clientName=servicepulse");
    return data;
  } catch (err) {
    console.log(err);
    return null;
  }
}
