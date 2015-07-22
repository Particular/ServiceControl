namespace ServiceControl.Recoverability.Groups.Archive
{
    using NServiceBus;

    public class ArchiveAllInGroup : ICommand
    {
        public string GroupId { get; set; }
    }
}
