namespace ServiceControl.Recoverability.Retries
{
    using NServiceBus;

    class NoopDequeuer : AdvancedDequeuer
    {
        protected override void HandleMessage(TransportMessage message)
        {
        }
    }
}