namespace ServiceControl.Recoverability.EventLog
{
    using MessageFailures.InternalMessages;
    using ServiceControl.EventLog;

    class ReclassificationOfErrorMessageCompleteDefinition : EventLogMappingDefinition<ReclassificationOfErrorMessageComplete>
    {
        public ReclassificationOfErrorMessageCompleteDefinition()
        {
            Description(m => $"Reclassification of {m.NumberofMessageReclassified} error messages without existing classification complete.");
        }
    }
}