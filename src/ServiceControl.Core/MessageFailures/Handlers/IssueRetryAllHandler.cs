namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using NServiceBus;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;
    using ServiceControl.Recoverability.Retries;

    public class IssueRetryAllHandler : IHandleMessages<RequestRetryAll>
    {
        public Retryer Retryer { get; set; }

        public void Handle(RequestRetryAll message)
        {
            string query = null;
            if (message.Endpoint != null)
            {
                query = String.Format("ReceivingEndpointname:{0}", message.Endpoint);
            }

            Retryer.StartRetryForIndex<FailedMessageViewIndex>(query);
        }
    }
}