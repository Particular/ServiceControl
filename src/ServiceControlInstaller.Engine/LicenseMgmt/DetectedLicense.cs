namespace ServiceControlInstaller.Engine.LicenseMgmt
{
    using System;
    using Particular.Licensing;

    public class DetectedLicense
    {
        public LicenseDetails Details;
        public string Location { get; set; }

        public DetectedLicense()
        {
            Details = new LicenseDetails();
        }

        public DetectedLicense(string licensePath, string licenseText)
        {
            Details = new LicenseDetails();

            Location = licensePath;
            Exception validationFailure;
            License license;
            
            if (!LicenseVerifier.TryVerify(licenseText, out validationFailure))
            {
                return;
            }

            if (!LicenseManager.TryDeserializeLicense(licenseText, out license))
            {
                return;
            }

            if (!license.ValidForApplication("ServiceControl"))
            {
                return;
            }
            Details = LicenseDetails.FromLicense(license);
        }
       
    }
}