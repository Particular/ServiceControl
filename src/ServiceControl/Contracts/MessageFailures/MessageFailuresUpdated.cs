namespace ServiceControl.Contracts.MessageFailures
{
    using System;
    using Infrastructure.DomainEvents;

    public class MessageFailuresUpdated : IDomainEvent
    {
        public MessageFailuresUpdated()
        {
            RaisedAt = DateTime.UtcNow;
        }

        [Obsolete]
        public int Total => UnresolvedTotal;  // Left here for backwards compatibility, to be removed eventually.

        public DateTime RaisedAt { get; set; }
        public int ArchivedTotal { get; set; }
        public int UnresolvedTotal { get; set; }
    }
}