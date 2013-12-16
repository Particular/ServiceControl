namespace ServiceControl.EventLog.Definitions
{
    using Contracts.CustomChecks;

    public class CustomCheckSucceededDefinition : EventLogMappingDefinition<CustomCheckSucceeded>
    {
        public CustomCheckSucceededDefinition()
        {
            TreatAsError();

            Description(m => string.Format("{0}: Working as expected.", m.CustomCheckId));

            RelatesToCustomCheck(m => m.CustomCheckId);

            RelatesToEndpoint(m => m.OriginatingEndpoint.Name);
            RelatesToMachine(m => m.OriginatingEndpoint.Machine);

            RaisedAt(m => m.SucceededAt);
        }
    }
}
