namespace ServiceControl.CustomChecks
{
    using NServiceBus;

    class CustomChecksUpdated : IEvent
    {
        public int Failed
        {
            get; set;
        }
    }
}