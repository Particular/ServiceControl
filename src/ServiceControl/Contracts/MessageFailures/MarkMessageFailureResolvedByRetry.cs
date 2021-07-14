namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;

    /// <summary>
    /// A command used before ServiceControl 4.20. Sent by the Audit instance to the Main instance when the Audit instances detected that the audited message has the ServiceControl.
    /// When upgrading from 4.x to 4.20 and higher these legacy messages may still be in the input queue so a handler for these is needed.
    /// retry header.
    /// </summary>
    public class MarkMessageFailureResolvedByRetry : ICommand
    {
        public string FailedMessageId { get; set; }
        public string[] AlternativeFailedMessageIds { get; set; }
    }
}