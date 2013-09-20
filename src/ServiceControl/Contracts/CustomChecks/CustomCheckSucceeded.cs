namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using NServiceBus;

    public class CustomCheckSucceeded : IEvent
    {
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public DateTime SucceededAt { get; set; }
    }
}
