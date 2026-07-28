namespace ServiceControl.Licensing
{
    using ServiceControl.LicenseManagement;

    public class LicenseInfo
    {
        public bool TrialLicense { get; set; }

        public string Edition { get; set; }

        public string RegisteredTo { get; set; }

        public string UpgradeProtectionExpiration { get; set; }

        public string ExpirationDate { get; set; }

        public string Status { get; set; }

        public LicensedProduct[] Products { get; set; }

        public string LicenseType { get; set; }

        public string InstanceName { get; set; }

        public string LicenseStatus { get; set; }

        public string LicenseExtensionUrl { get; set; }
    }
}
