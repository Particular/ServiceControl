namespace ServiceControlInstaller.Engine.LicenseMgmt
{
    public class DetectedLicense
    {
        public LicenseDetails Details;
        public string Location { get; set; }

        public DetectedLicense()
        {
            Details = new LicenseDetails();
        }

        public DetectedLicense(string licensePath, LicenseDetails details) : this()
        {
            Location = licensePath;
            Details = details;
        }
    }
}