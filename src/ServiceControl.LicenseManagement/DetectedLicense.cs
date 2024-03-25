namespace ServiceControl.LicenseManagement
{
    public class DetectedLicense
    {
        public DetectedLicense(string licensePath, LicenseDetails details)
        {
            Location = licensePath;
            Details = details;
        }

        public string Location { get; }
        public LicenseDetails Details { get; }
        public bool IsEvaluationLicense { get; init; }
    }
}