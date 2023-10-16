namespace ServiceControl.MessageFailures.InternalMessages
{
    using System;
    using NServiceBus;

    class UnArchiveMessagesByRange : ICommand
    {
        public DateTime To { get; set; }
        public DateTime From { get; set; }
    }
}