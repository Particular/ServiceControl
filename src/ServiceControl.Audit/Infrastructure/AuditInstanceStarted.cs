namespace ServiceControl.Contracts
{
    using ServiceControl.Infrastructure.DomainEvents;
    using System.Collections.Generic;

    public class AuditInstanceStarted : IDomainEvent, IBusEvent
    {
        public Dictionary<string,string> StartUpDetails { get; set; }
    }
}
