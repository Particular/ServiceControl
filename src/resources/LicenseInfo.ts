export default interface LicenseInfo {
  registered_to: string;
  edition: string;
  expiration_date: string;
  upgrade_protection_expiration: string;
  license_type: string;
  instance_name: string;
  trial_license: boolean;
  license_status: LicenseStatus;
  license_extension_url?: string;
  status: string;
}

export enum LicenseStatus {
  Valid = "Valid",
  Unavailable = "Unavailable",
  InvalidDueToExpiredSubscription = "InvalidDueToExpiredSubscription",
  ValidWithExpiringTrial = "ValidWithExpiringTrial",
  InvalidDueToExpiredTrial = "InvalidDueToExpiredTrial",
  InvalidDueToExpiredUpgradeProtection = "InvalidDueToExpiredUpgradeProtection",
  ValidWithExpiredUpgradeProtection = "ValidWithExpiredUpgradeProtection",
  ValidWithExpiringUpgradeProtection = "ValidWithExpiringUpgradeProtection",
  ValidWithExpiringSubscription = "ValidWithExpiringSubscription",
}
