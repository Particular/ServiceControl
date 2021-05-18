namespace ServiceControl.Recoverability
{
    using NServiceBus;

    class UnarchiveAllInGroup : ICommand
    {
        public string GroupId { get; set; }
    }
}