namespace ServiceControl.MessageFailures.InternalMessages
{
    using System.Collections.Generic;
    using NServiceBus;

    class UnArchiveMessages : ICommand
    {
        public List<string> FailedMessageIds { get; set; }
    }
}