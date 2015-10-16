namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Faults;

    class NoopDequeuer : AdvancedDequeuer
    {
        protected override void HandleMessage(TransportMessage message)
        {
        }

        protected override IManageMessageFailures FaultManager
        {
            get { return null; }
        }
    }
}