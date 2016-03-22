﻿namespace ServiceControl.Contracts.MessageFailures
{
    using System;
    using NServiceBus;

    public class MessageFailuresUpdated : IEvent
    {
        public MessageFailuresUpdated()
        {
            RaisedAt = DateTime.UtcNow;
        }

        public int Total { get; set; }
        public DateTime RaisedAt { get; set; }
        public int ArchivedTotal { get; set; }
        public int UnresolvedTotal { get; set; }
    }
}