namespace ServiceControl.CustomChecks
{
    using System;
    using NServiceBus;

    class DeleteCustomCheck : ICommand
    {
        public Guid Id { get; set; }
    }

    class CustomCheckDeleted : IEvent
    {
        public Guid Id { get; set; }        
    }
}
