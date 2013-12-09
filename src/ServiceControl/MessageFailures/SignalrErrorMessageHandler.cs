namespace ServiceControl.MessageFailures
{
    using Contracts.Operations;
    using NServiceBus;

    class SignalrErrorMessageHandler : IHandleMessages<ImportFailedMessage>
    {
        public InMemoryErrorMessagesCounterCache InMemoryErrorMessagesCounterCache { get; set; }

        public IBus Bus { get; set; }

        public void Handle(ImportFailedMessage message)
        {
            var total = InMemoryErrorMessagesCounterCache.Increment();

            Bus.Publish(new TotalErrorMessagesUpdated {Total = total});
        }
    }
}