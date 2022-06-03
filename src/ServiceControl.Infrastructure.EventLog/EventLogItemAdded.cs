﻿namespace ServiceControl.Contracts.EventLog
{
    using System;
    using System.Collections.Generic;
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;
    using ServiceControl.EventLog;

    public class EventLogItemAdded : IUserInterfaceEvent, IDomainEvent
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public Severity Severity { get; set; }
        public DateTime RaisedAt { get; set; }
        public ICollection<string> RelatedTo { get; set; } // This could be the Id of a related document, such as the FailedMessage event, which will have more information regarding this alert.
        public string Category { get; set; }
    }
}