namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class ArchiveAllInGroup : ICommand
    {
        public string GroupId { get; set; }
    }
}