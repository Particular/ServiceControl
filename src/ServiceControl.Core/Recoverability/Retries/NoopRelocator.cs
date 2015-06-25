namespace ServiceControl.Recoverability.Retries
{
    using NServiceBus;

    class NoopRelocator : Relocator
    {
        protected override void HandleMessage(TransportMessage message)
        {
        }
    }
}