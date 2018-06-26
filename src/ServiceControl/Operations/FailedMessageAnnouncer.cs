namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure.DomainEvents;

    class FailedMessageAnnouncer
    {
        private IDomainEvents domainEvents;

        public FailedMessageAnnouncer(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public Task Announce(IReadOnlyDictionary<string, string> headers, FailureDetails failureDetails)
        {
            var failingEndpointId = headers.ProcessingEndpointName();

            string failedMessageId;
            if (headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out failedMessageId))
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
                FailedMessageId = headers.UniqueId(),
            });
        }
    }
}