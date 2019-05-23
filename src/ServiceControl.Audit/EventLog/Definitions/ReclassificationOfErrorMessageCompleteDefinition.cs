namespace ServiceControl.EventLog.Definitions
{
    using MessageFailures.InternalMessages;

    class ReclassificationOfErrorMessageCompleteDefinition : EventLogMappingDefinition<ReclassificationOfErrorMessageComplete>
    {
        public ReclassificationOfErrorMessageCompleteDefinition()
        {
            Description(m => $"Reclassification of {m.NumberofMessageReclassified} error messages without existing classification complete.");
        }
    }
}