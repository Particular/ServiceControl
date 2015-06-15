namespace ServiceControl.ExceptionGroups.Retry
{
    using NServiceBus;

    public class RetryAllForGroup : ICommand
    {
        public string GroupId { get; set; }
    }
}
