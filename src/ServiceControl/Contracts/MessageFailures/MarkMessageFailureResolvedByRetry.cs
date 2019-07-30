namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;

    public class MarkMessageFailureResolvedByRetry : ICommand
    {
        public string FailedMessageId { get; set; }
        public string[] AlternativeFailedMessageIds { get; set; }
    }
}