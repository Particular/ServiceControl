namespace ServiceControl.Recoverability.Groups.OldFailureGrouping
{
    using NServiceBus;

    public class GroupOldFailures : ICommand
    {
        public string BatchId { get; set; }
    }
}