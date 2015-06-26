namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using NServiceBus;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;
    using ServiceControl.Recoverability.Retries;

    public class IssueRetryAllHandler : IHandleMessages<RequestRetryAll>
    {
        public Retryer Retryer { get; set; }

        public void Handle(RequestRetryAll message)
        {
            if (message.Endpoint != null)
            {
                Retryer.StartRetryForIndex<FailedMessageViewIndex>(m => m.ProcessingAttempts.Last().ProcessingEndpoint.Name == message.Endpoint);
            }
            else
            {
                Retryer.StartRetryForIndex<FailedMessageViewIndex>();
            }
        }
    }
}