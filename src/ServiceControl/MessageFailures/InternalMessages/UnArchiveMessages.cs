namespace ServiceControl.MessageFailures.InternalMessages
{
    using System.Collections.Generic;
    using NServiceBus;

    public class UnArchiveMessages : ICommand
    {
        public List<string> FailedMessageIds { get; set; }
    }
}