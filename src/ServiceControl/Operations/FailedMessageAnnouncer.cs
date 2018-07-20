namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Contracts.Operations;
    using Infrastructure.DomainEvents;

    class FailedMessageAnnouncer
    {
        IDomainEvents domainEvents;

        public FailedMessageAnnouncer(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public Task Announce(IReadOnlyDictionary<string, string> headers, FailureDetails failureDetails)
        {
            var failingEndpointId = headers.ProcessingEndpointName();

            if (headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out var failedMessageId))
            {
                return domainEvents.Raise(new MessageFailed
                {
                    FailureDetails = failureDetails,
                    EndpointId = failingEndpointId,
                    FailedMessageId = failedMessageId,
                    RepeatedFailure = true
                });
            }

            return domainEvents.Raise(new MessageFailed
            {
                FailureDetails = failureDetails,
                EndpointId = failingEndpointId,
                FailedMessageId = headers.UniqueId()
            });
        }
    }
}