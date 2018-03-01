namespace ServiceControl.CustomChecks
{
    using System;
    using NServiceBus;
    using ServiceControl.Infrastructure.DomainEvents;

    class DeleteCustomCheck : ICommand
    {
        public Guid Id { get; set; }
    }

    class CustomCheckDeleted : IDomainEvent
    {
        public Guid Id { get; set; }
    }
}
