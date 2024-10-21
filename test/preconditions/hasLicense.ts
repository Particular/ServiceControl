import { activeLicenseResponse } from "../mocks/license-response-template";
import { SetupFactoryOptions } from "../driver";
import LicenseInfo, { LicenseStatus } from "@/resources/LicenseInfo";
import { useLicense } from "@/composables/serviceLicense";

const { license } = useLicense();

export const hasActiveLicense = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}license`, {
    body: activeLicenseResponse,
  });
  return activeLicenseResponse;
};

export enum LicenseType {
  Subscription,
  Trial,
  UpgradeProtection,
}

export const hasExpiredLicense = (licenseType: LicenseType, licenseExtensionUrl: string = "https://particular.net/extend-your-trial?p=servicepulse") => createLicenseMockedResponse(licenseType, false, licenseExtensionUrl);
export const hasExpiringLicense = (licenseType: LicenseType, licenseExtensionUrl: string = "https://particular.net/extend-your-trial?p=servicepulse") => createLicenseMockedResponse(licenseType, true, licenseExtensionUrl);

const createLicenseMockedResponse =
  (liceseType: LicenseType, expiring = false, licenseExtensionUrl: string) =>
  ({ driver }: SetupFactoryOptions) => {
    const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
    let status: LicenseStatus;

    switch (liceseType) {
      case LicenseType.Subscription:
        status = expiring ? LicenseStatus.ValidWithExpiringSubscription : LicenseStatus.InvalidDueToExpiredSubscription;
        break;
      case LicenseType.Trial:
        status = expiring ? LicenseStatus.ValidWithExpiringTrial : LicenseStatus.InvalidDueToExpiredTrial;
        break;
      case LicenseType.UpgradeProtection:
        status = expiring ? LicenseStatus.ValidWithExpiringUpgradeProtection : LicenseStatus.InvalidDueToExpiredUpgradeProtection;
        break;
    }

    //We need to reset the global state to ensure the warning toast is always triggered by the value changing between multiple test runs. See documented issue and proposed solution https://github.com/Particular/ServicePulse/issues/1905
    license.license_status = LicenseStatus.Unavailable;

    driver.mockEndpoint(`${serviceControlInstanceUrl}license`, {
      body: <LicenseInfo>{
        registered_to: "ACME Software",
        edition: "Enterprise",
        expiration_date: "",
        upgrade_protection_expiration: "2050-01-01T00:00:00.0000000Z",
        license_type: status === LicenseStatus.ValidWithExpiringTrial || status === LicenseStatus.InvalidDueToExpiredTrial ? "Trial" : "Commercial",
        instance_name: "Particular.ServiceControl",
        trial_license: status === LicenseStatus.ValidWithExpiringTrial || status === LicenseStatus.InvalidDueToExpiredTrial,
        license_status: status,
        license_extension_url: licenseExtensionUrl,
      },
    });
  };
