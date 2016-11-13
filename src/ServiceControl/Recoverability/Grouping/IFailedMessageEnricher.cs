namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;

    public interface IFailedMessageEnricher
    {
        IEnumerable<FailedMessage.FailureGroup> Enrich(string messageType, FailureDetails failureDetails);
    }
}