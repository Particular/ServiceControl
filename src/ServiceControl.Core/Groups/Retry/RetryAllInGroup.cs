namespace ServiceControl.Groups.Retry
{
    using System;
    using NServiceBus;

    public class RetryAllInGroup : ICommand
    {
        public string GroupId { get; set; }
        public DateTimeOffset StartedAt { get; set; }
    }
}
