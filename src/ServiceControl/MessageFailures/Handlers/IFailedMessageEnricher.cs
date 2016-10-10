namespace ServiceControl.MessageFailures.Handlers
{
    using System.Collections.Generic;
    using ServiceControl.Contracts.Operations;

    public interface IFailedMessageEnricher
    {
        IEnumerable<FailedMessage.FailureGroup> Enrich(FailureDetails details);
    }
}