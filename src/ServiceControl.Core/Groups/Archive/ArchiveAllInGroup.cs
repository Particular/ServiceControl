namespace ServiceControl.Groups.Archive
{
    using NServiceBus;

    public class ArchiveAllInGroup : ICommand
    {
        public string GroupId { get; set; }
    }
}
