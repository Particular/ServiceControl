namespace ServiceControl.MessageFailures.Handlers
{
    using NServiceBus;
    using ServiceControl.MessageFailures.InternalMessages;
    using ServiceControl.Recoverability.Retries;

    class RetryMessagesByIdHandler : IHandleMessages<RetryMessagesById>
    {
        public Retryer Retryer { get; set; }

        public void Handle(RetryMessagesById message)
        {
            Retryer.StageRetryByUniqueMessageIds(message.MessageUniqueIds);
        }
    }
}
