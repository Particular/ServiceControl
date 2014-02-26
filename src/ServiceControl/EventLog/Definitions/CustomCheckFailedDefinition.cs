namespace ServiceControl.EventLog.Definitions
{
    using Contracts.CustomChecks;

    public class CustomCheckFailedDefinition : EventLogMappingDefinition<CustomCheckFailed>
    {
        public CustomCheckFailedDefinition()
        {
            TreatAsError();

            Description(m=> string.Format("{0}: {1}", m.CustomCheckId, m.FailureReason));

            RelatesToEndpoint(m => m.OriginatingEndpoint.Name);
            RelatesToMachine(m => m.OriginatingEndpoint.Host);

            RaisedAt(m => m.FailedAt);
        }
    }
}
