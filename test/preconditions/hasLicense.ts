import { activeLicenseResponse } from "../mocks/license-response-template";
import { SetupFactoryOptions } from "../driver";
import LicenseInfo, { LicenseStatus } from "@/resources/LicenseInfo";

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

export const hasExpiredLicense = (licenseType: LicenseType) => createLicenseMockedResponse(licenseType, false);
export const hasExpiringLicense = (licenseType: LicenseType) => createLicenseMockedResponse(licenseType, true);

const createLicenseMockedResponse =
  (liceseType: LicenseType, expiring = false) =>
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

    driver.mockEndpoint(`${serviceControlInstanceUrl}license`, {
      body: <LicenseInfo>{
        registered_to: "ACME Software",
        edition: "Enterprise",
        expiration_date: "",
        upgrade_protection_expiration: "2050-01-01T00:00:00.0000000Z",
        license_type: status == LicenseStatus.ValidWithExpiringTrial || status == LicenseStatus.InvalidDueToExpiredTrial ?  "Trial": "Subscription",
        instance_name: "Particular.ServiceControl",
        trial_license: status === LicenseStatus.ValidWithExpiringTrial || status === LicenseStatus.InvalidDueToExpiredTrial,
        license_status: status,
      },
    });
  };
