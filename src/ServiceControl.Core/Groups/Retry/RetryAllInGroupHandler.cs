namespace ServiceControl.Groups.Retry
{
    using System;
    using System.Linq;
    using NServiceBus;
    using Raven.Client;

    using ServiceControl.Groups.Indexes;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.InternalMessages;

    public class RetryAllInGroupHandler : IHandleMessages<RetryAllInGroup>
    {
        public void Handle(RetryAllInGroup message)
        {
            if (String.IsNullOrWhiteSpace(message.GroupId))
            {
                return;
            }

            var query = Session.Query<MessageFailureHistory, MessageFailuresByFailureGroupsIndex>()
                .Where(m => m.FailureGroups.Any(g => g.Id == message.GroupId));

            using (var stream = Session.Advanced.Stream((query)))
            {
                while (stream.MoveNext())
                {
                    // We must send one Retry Message per item being retried in order to trigger the Retry Policy Saga
                    var retryMessage = new RetryMessage { FailedMessageId = stream.Current.Document.UniqueMessageId };

                    Bus.SendLocal(retryMessage);
                }
            }

            // At this point all messages have been queued for retry but no retry has actually occurred 
            Bus.Publish<FailedMessageGroupRetried>(m => m.GroupId = message.GroupId);
        }

        public IBus Bus { get; set; }
        public IDocumentSession Session { get; set; }
    }
}