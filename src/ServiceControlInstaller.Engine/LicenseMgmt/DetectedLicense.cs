namespace ServiceControlInstaller.Engine.LicenseMgmt
{
    public class DetectedLicense
    {
        public DetectedLicense()
        {
            Details = new LicenseDetails();
        }

        public DetectedLicense(string licensePath, LicenseDetails details) : this()
        {
            Location = licensePath;
            Details = details;
        }

        public string Location { get; set; }
        public LicenseDetails Details;
    }
}