namespace ServiceControl.MessageFailures
{
    using Contracts.Operations;
    using NServiceBus;

    class SignalrErrorMessageHandler : IHandleMessages<FailedMessageDetected>
    {
        public InMemoryErrorMessagesCounterCache InMemoryErrorMessagesCounterCache { get; set; }

        public IBus Bus { get; set; }

        public void Handle(FailedMessageDetected message)
        {
            var total = InMemoryErrorMessagesCounterCache.Increment();

            Bus.Publish(new TotalErrorMessagesUpdated {Total = total});
        }
    }
}