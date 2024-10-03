namespace ServiceControl.Persistence
{
    using System;

    public class TrialMetadata
    {
        public DateOnly TrialStartDate { get; set; }

        public static string TrialMetadataId = "metadata/trialinformation";
    }
}