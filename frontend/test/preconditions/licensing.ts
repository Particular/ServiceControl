import { SetupFactoryOptions } from "../driver";
import LicenseInfo, { LicenseStatus, LicenseType } from "@/resources/LicenseInfo";
import { getDefaultConfig } from "@/defaultConfig";

const licenseResponseTemplate = <LicenseInfo>{
  registered_to: "ACME Software",
  edition: "Enterprise",
  expiration_date: "2026-01-23T00:00:00.0000000Z",
  upgrade_protection_expiration: "",
  license_type: "Commercial",
  instance_name: "Particular.ServiceControl",
  trial_license: false,
  license_status: LicenseStatus.Valid,
  license_extension_url: "",
  status: "valid",
};
export const hasActiveLicense = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = getDefaultConfig().service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}license`, {
    body: licenseResponseTemplate,
  });
  return licenseResponseTemplate;
};
export const hasExpiredLicense = (licenseType: LicenseType, expiredDays: number = 10, extensionUrl: string = "") => getLicenseMockedResponse(licenseType, expiredDays, extensionUrl, true);
export const hasExpiringLicense = (licenseType: LicenseType, expiringInDays: number = 10, extensionUrl: string = "") => getLicenseMockedResponse(licenseType, expiringInDays, extensionUrl, false);

const getLicenseMockedResponse =
  (licenseType: LicenseType, expiringInDays: number, extensionUrl: string, isExpired: boolean) =>
  ({ driver }: SetupFactoryOptions) => {
    const serviceControlInstanceUrl = getDefaultConfig().service_control_url;
    const customISOString = getCustomDateISOString(expiringInDays, isExpired);

    let status: LicenseStatus;
    let trialLicense = false;
    let upgradeProtectionExpiration = "";
    let expirationDate = "";
    let licenseExtensionUrl = extensionUrl;

    switch (licenseType) {
      case LicenseType.Subscription:
        status = isExpired ? LicenseStatus.InvalidDueToExpiredSubscription : LicenseStatus.ValidWithExpiringSubscription;
        expirationDate = customISOString;
        break;
      case LicenseType.Trial:
        status = isExpired ? LicenseStatus.InvalidDueToExpiredTrial : LicenseStatus.ValidWithExpiringTrial;
        expirationDate = customISOString;
        trialLicense = true;
        licenseExtensionUrl = extensionUrl ? extensionUrl : "https://particular.net/extend-your-trial?p=servicepulse";
        break;
      case LicenseType.UpgradeProtection:
        status = isExpired ? LicenseStatus.InvalidDueToExpiredUpgradeProtection : LicenseStatus.ValidWithExpiringUpgradeProtection;
        upgradeProtectionExpiration = customISOString;
        licenseExtensionUrl = extensionUrl ? extensionUrl : "https://particular.net/extend-your-trial?p=servicepulse";
        break;
    }

    const response = {
      ...licenseResponseTemplate,
      license_type: status === LicenseStatus.ValidWithExpiringTrial || status === LicenseStatus.InvalidDueToExpiredTrial ? "Trial" : "Commercial",
      trial_license: trialLicense,
      expiration_date: expirationDate,
      upgrade_protection_expiration: upgradeProtectionExpiration,
      license_status: status,
      license_extension_url: licenseExtensionUrl,
    };
    console.log(response);
    driver.mockEndpoint(`${serviceControlInstanceUrl}license`, {
      body: response,
    });
    return response;
  };

function getCustomDateISOString(daysCount: number, isExpired: boolean) {
  const today = new Date();
  const customDate = new Date(today);
  // Set hours, minutes, seconds, and milliseconds to 00
  today.setHours(0, 0, 0, 0);
  customDate.setHours(0, 0, 0, 0);

  if (isExpired) {
    customDate.setDate(today.getDate() - daysCount);
  } else {
    customDate.setDate(today.getDate() + daysCount);
  }

  const nativeISOString = customDate.toISOString(); // e.g., "2026-02-02T14:23:45.123Z"
  const customISOString = nativeISOString.replace(/\.\d+Z$/, (match) => match.slice(0, -1).padEnd(8, "0") + "Z");
  return customISOString;
}
