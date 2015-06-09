namespace ServiceControl.InternalContracts.Messages.MessageFailures
{
    using NServiceBus;

    public class RaiseNewFailureGroupDetectedEvent : ICommand
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
    }
}
