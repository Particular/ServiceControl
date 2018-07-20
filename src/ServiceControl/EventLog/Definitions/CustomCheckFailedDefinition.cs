namespace ServiceControl.EventLog.Definitions
{
    using Contracts.CustomChecks;

    public class CustomCheckFailedDefinition : EventLogMappingDefinition<CustomCheckFailed>
    {
        public CustomCheckFailedDefinition()
        {
            TreatAsError();

            Description(m => $"{m.CustomCheckId}: {m.FailureReason}");

            RelatesToEndpoint(m => m.OriginatingEndpoint.Name);
            RelatesToHost(m => m.OriginatingEndpoint.HostId);
            RelatesToCustomCheck(m => m.CustomCheckId);

            RaisedAt(m => m.FailedAt);
        }
    }
}