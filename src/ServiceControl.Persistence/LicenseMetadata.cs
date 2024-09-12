namespace ServiceControl.Persistence
{
    using System;

    public class LicenseMetadata
    {
        public DateOnly TrialStartDate { get; set; }

        public static string LicenseMetadataId = "metadata/origination";
    }
}