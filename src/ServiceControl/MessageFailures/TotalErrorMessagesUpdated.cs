namespace ServiceControl.MessageFailures
{
    using NServiceBus;

    class TotalErrorMessagesUpdated : IEvent
    {
        public int Total
        {
            get; set;
        }
    }
}