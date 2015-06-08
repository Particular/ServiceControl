namespace ServiceControl.CustomChecks
{
    using System;
    using NServiceBus;

    public  class DeleteCustomCheck : ICommand
    {
        public Guid Id { get; set; }
    }

    public class CustomCheckDeleted : IEvent
    {
        public Guid Id { get; set; }        
    }
}
