namespace ServiceControl.ExceptionGroups.Archive
{
    using NServiceBus;

    public class ArchiveAllForGroup : ICommand
    {
        public string GroupId { get; set; }
    }
}
