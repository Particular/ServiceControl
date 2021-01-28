namespace ServiceControl.LicenseManagement
{
    using System;
    using Particular.Licensing;

    public class LicenseManager
    {
        public static DetectedLicense FindLicense()
        {
            var sources = new LicenseSource[]
            {
                new LicenseSourceFilePath(GetMachineLevelLicenseLocation())
            };

            var result = ActiveLicense.Find("ServiceControl", sources);

            var detectedLicense = new DetectedLicense(result.Location, LicenseDetails.FromLicense(result.License))
            {
                IsEvaluationLicense = string.Equals(result.Location, "Trial License", StringComparison.OrdinalIgnoreCase)
            };

            return detectedLicense;
        }

        public static bool IsLicenseValidForServiceControlInit(DetectedLicense license, out string errorMessage)
        {
            if (!license.Details.ValidForServiceControl)
            {
                errorMessage = "License is not for ServiceControl";
                return false;
            }

            if (license.Details.HasLicenseExpired())
            {
                errorMessage = "License has expired";
                return false;
            }

            // E.g. Cannot set up a new instance of ServiceControl on a trial license when running in a docker container
            if (IsRunningInDocker && license.IsEvaluationLicense)
            {
                errorMessage = "Cannot run ServiceControl in a container with a trial license";
                return false;
            }

            errorMessage = "";
            return true;
        }

        public static bool IsLicenseValidForServiceControlInit(string licenseText, out string errorMessage)
        {
            if (!TryDeserializeLicense(licenseText, out var license))
            {
                errorMessage = "Invalid license file";
                return false;
            }

            return IsLicenseValidForServiceControlInit(new DetectedLicense("", LicenseDetails.FromLicense(license)), out errorMessage);
        }

        public static bool TryImportLicenseFromText(string licenseText, out string errorMessage)
        {
            if (!LicenseVerifier.TryVerify(licenseText, out _))
            {
                errorMessage = "Invalid license file";
                return false;
            }

            if (!TryDeserializeLicense(licenseText, out var license))
            {
                errorMessage = "Invalid license file";
                return false;
            }

            if (!license.ValidForApplication("ServiceControl"))
            {
                errorMessage = "License is not for ServiceControl";
                return false;
            }

            if (license.HasExpired())
            {
                errorMessage = "Failed to import because the license has expired";
                return false;
            }

            try
            {
                new FilePathLicenseStore().StoreLicense(GetMachineLevelLicenseLocation(), licenseText);
            }
            catch (Exception)
            {
                errorMessage = "Failed to import license into the filesystem";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public static bool TryImportLicense(string licenseFile, out string errorMessage)
        {
            var licenseText = NonBlockingReader.ReadAllTextWithoutLocking(licenseFile);
            return TryImportLicenseFromText(licenseText, out errorMessage);
        }

        static string GetMachineLevelLicenseLocation()
        {
            return LicenseFileLocationResolver.GetPathFor(Environment.SpecialFolder.CommonApplicationData);
        }

        static bool IsRunningInDocker => bool.TryParse(Environment.GetEnvironmentVariable("SERVICECONTROL_RUNNING_IN_DOCKER"), out bool isDocker) && isDocker;

        static bool TryDeserializeLicense(string licenseText, out License license)
        {
            license = null;
            try
            {
                license = LicenseDeserializer.Deserialize(licenseText);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
