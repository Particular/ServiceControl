namespace ServiceControl.CustomChecks
{
    using System;
    using NServiceBus;

    public class CustomChecksUpdated : IEvent
    {
        public CustomChecksUpdated()
        {
            RaisedAt = DateTime.UtcNow;
        }

        public int Failed { get; set; }
        public DateTime RaisedAt { get; set; }
    }
}