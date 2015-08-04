namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.MessageFailures.InternalMessages;

    class ReclassificationOfErrorMessageCompleteDefinition : EventLogMappingDefinition<ReclassificationOfErrorMessageComplete>
    {
        public ReclassificationOfErrorMessageCompleteDefinition()
        {
            Description(m => string.Format("Reclassification of {0} error messages without existing classification complete.", m.NumberofMessageReclassified));
        }
    }
}