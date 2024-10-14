namespace ServiceControl.Persistence
{
    using System;

    public class TrialMetadata
    {
        public DateOnly TrialEndDate { get; set; }

        public static string TrialMetadataId = "metadata/trialinformation";
    }
}