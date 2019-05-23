namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using Contracts.Operations;
    using MessageFailures;

    public interface IFailedMessageEnricher
    {
        IEnumerable<FailedMessage.FailureGroup> Enrich(string messageType, FailureDetails failureDetails, FailedMessage.ProcessingAttempt processingAttempt);
    }
}