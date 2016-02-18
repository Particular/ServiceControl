namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;

    public class ArchiveAllInGroup : ICommand
    {
        public string GroupId { get; set; }
        public DateTime? CutOff { get; set; }
    }
}