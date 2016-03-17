namespace ServiceControl.MessageFailures.InternalMessages
{
    using System;
    using NServiceBus;

    public class UnArchiveMessagesByRange : ICommand
    {
        public DateTime To { get; set; }
        public DateTime From { get; set; }
        public DateTime CutOff { get; set; }
    }
}