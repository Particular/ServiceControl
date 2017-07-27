namespace ServiceControl.EventLog.Definitions
{
    using Contracts.MessageFailures;

    public class MessageFailedDefinition : EventLogMappingDefinition<MessageFailed>
    {
        public MessageFailedDefinition()
        {
            TreatAsError();

            Description(m=>m.FailureDetails.Exception.Message);

            RelatesToMessage(m => m.FailedMessageId);
            RelatesToEndpoint(m => m.EndpointId);

            RaisedAt(m => m.FailureDetails.TimeOfFailure);
        }
    }
}
