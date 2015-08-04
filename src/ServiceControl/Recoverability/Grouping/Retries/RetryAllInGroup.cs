namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class RetryAllInGroup : ICommand
    {
        public string GroupId { get; set; }
    }
}