﻿namespace ServiceControl.Recoverability.Groups.Retry
{
    using NServiceBus;

    public class RetryAllInGroup : ICommand
    {
        public string GroupId { get; set; }
    }
}