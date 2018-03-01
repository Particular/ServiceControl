namespace ServiceControl.Contracts.MessageFailures
{
    using System;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    public class MessageFailuresUpdated : IDomainEvent, IUserInterfaceEvent
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