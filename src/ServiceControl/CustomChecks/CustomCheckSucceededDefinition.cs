namespace ServiceControl.CustomChecks
{
    using Contracts.CustomChecks;
    using EventLog;

    class CustomCheckSucceededDefinition : EventLogMappingDefinition<CustomCheckSucceeded>
    {
        public CustomCheckSucceededDefinition()
        {
            Description(m => $"{m.CustomCheckId}: Working as expected.");

            RelatesToCustomCheck(m => m.CustomCheckId);

            RelatesToEndpoint(m => m.OriginatingEndpoint.Name);
            RelatesToHost(m => m.OriginatingEndpoint.HostId);

            RaisedAt(m => m.SucceededAt);
        }
    }
}