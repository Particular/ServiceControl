namespace ServiceControl.MessageFailures.Handlers
{
    using Contracts.MessageFailures;
    using NServiceBus;

    public class RaiseMessageFailuresChanges : IHandleMessages<MessageSubmittedForRetry>, IHandleMessages<MessageFailed>
    {
        readonly IBus bus;
        public MessageFailuresComputation MessageFailuresComputation { get; set; }

        public RaiseMessageFailuresChanges(IBus bus)
        {
            this.bus = bus;
        }

        public void Handle(MessageSubmittedForRetry message)
        {
            bus.Publish(new MessageFailuresUpdated {Total = MessageFailuresComputation.MessageBeingResolved()});
        }

        public void Handle(MessageFailed message)
        {
            bus.Publish(new MessageFailuresUpdated {Total = MessageFailuresComputation.MessageFailed()});
        }
    }
}