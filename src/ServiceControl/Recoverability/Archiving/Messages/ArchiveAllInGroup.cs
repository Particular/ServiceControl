namespace ServiceControl.Recoverability
{
    using NServiceBus;

    class ArchiveAllInGroup : ICommand
    {
        public string GroupId { get; set; }
    }
}