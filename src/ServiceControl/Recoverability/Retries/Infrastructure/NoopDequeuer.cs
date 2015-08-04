namespace ServiceControl.Recoverability
{
    using NServiceBus;

    class NoopDequeuer : AdvancedDequeuer
    {
        protected override void HandleMessage(TransportMessage message)
        {
        }
    }
}