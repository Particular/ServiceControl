namespace ServiceControl.CustomChecks
{
    using NServiceBus;

    class TotalCustomCheckUpdated : IEvent
    {
        public int Total
        {
            get; set;
        }
    }
}