import LicenseInfo, { LicenseStatus } from "@/resources/LicenseInfo";

export const activeLicenseResponse = <LicenseInfo>{
  registered_to: "ACME Software",
  edition: "Enterprise",
  expiration_date: "",
  upgrade_protection_expiration: "2050-01-01T00:00:00.0000000Z",
  license_type: "Commercial",
  instance_name: "Particular.ServiceControl",
  trial_license: false,
  license_status: LicenseStatus.Valid,
  status: "valid",
};
