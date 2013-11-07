namespace ServiceControl.MessageFailures
{
    using NServiceBus;

    class ErrorMessageCounterRegister : INeedInitialization
    {
        public void Init()
        {
            Configure.Component<InMemoryErrorMessagesCounterCache>(DependencyLifecycle.SingleInstance);
        }
    }
}