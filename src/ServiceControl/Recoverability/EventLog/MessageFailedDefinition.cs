namespace ServiceControl.Recoverability.EventLog
{
    using Contracts.MessageFailures;
    using ServiceControl.EventLog;

    class MessageFailedDefinition : EventLogMappingDefinition<MessageFailed>
    {
        public MessageFailedDefinition()
        {
            TreatAsError();

            Description(m => m.FailureDetails.Exception.Message);

            RelatesToMessage(m => m.FailedMessageId);
            RelatesToEndpoint(m => m.EndpointId);

            RaisedAt(m => m.FailureDetails.TimeOfFailure);
        }
    }
}