namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;

    public class RetryAllInGroup : ICommand
    {
        public string GroupId { get; set; }

        public DateTime? Started { get; set; }
    }
}