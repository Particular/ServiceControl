namespace ServiceControl.LicenseManagement
{
    using System;
    using Particular.Licensing;

    public class LicenseManager
    {
        public static DetectedLicense FindLicense()
        {
            var sources = LicenseSource.GetStandardLicenseSources();
            var result = ActiveLicense.Find("ServiceControl", sources.ToArray());

            var detectedLicense = new DetectedLicense(result.Location, LicenseDetails.FromLicense(result.License))
            {
                IsEvaluationLicense = string.Equals(result.Location, "Trial License", StringComparison.OrdinalIgnoreCase)
            };

            return detectedLicense;
        }

        public static bool TryImportLicense(string licenseFile, out string errorMessage)
        {
            var license = new LicenseSourceFilePath(licenseFile);
            var result = license.Find("ServiceControl");

            if (result.License is null)
            {
                errorMessage = result.Result;
                return false;
            }

            if (result.License.HasExpired())
            {
                errorMessage = "Failed to import because the license has expired";
                return false;
            }

            try
            {
                var licenseText = NonBlockingReader.ReadAllTextWithoutLocking(licenseFile);
                var machineLevelLicenseLocation = LicenseFileLocationResolver.GetPathFor(Environment.SpecialFolder.CommonApplicationData);
                FilePathLicenseStore.StoreLicense(machineLevelLicenseLocation, licenseText);
            }
            catch (Exception)
            {
                errorMessage = "Failed to import license into the filesystem";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public static DateTime GetReleaseDate() => ReleaseDateReader.GetReleaseDate();
    }
}