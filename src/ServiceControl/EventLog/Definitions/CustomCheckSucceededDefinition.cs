namespace ServiceControl.EventLog.Definitions
{
    using Contracts.CustomChecks;

    public class CustomCheckSucceededDefinition : EventLogMappingDefinition<CustomCheckSucceeded>
    {
        public CustomCheckSucceededDefinition()
        {
            Description(m => string.Format("{0}: Working as expected.", m.CustomCheckId));

            RelatesToCustomCheck(m => m.CustomCheckId);

            RelatesToEndpoint(m => m.OriginatingEndpoint.Name);
            RelatesToHost(m => m.OriginatingEndpoint.HostId);

            RaisedAt(m => m.SucceededAt);
        }
    }
}
