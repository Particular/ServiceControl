namespace ServiceControl.CustomChecks
{
    using System;
    using Infrastructure.DomainEvents;
    using NServiceBus;

    class DeleteCustomCheck : ICommand
    {
        public Guid Id { get; set; }
    }

    public class CustomCheckDeleted : IDomainEvent
    {
        public Guid Id { get; set; }
    }
}