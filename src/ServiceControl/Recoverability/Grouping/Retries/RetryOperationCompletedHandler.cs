namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class RetryOperationCompletedHandler : IHandleMessages<RetryOperationCompleted>
    {
        public Settings Settings { get; set; }
        public IDocumentStore Store { get; set; }
        
        public void Handle(RetryOperationCompleted message)
        {
            using (var session = Store.OpenSession())
            {
                var retryHistory = session.Load<RetryHistory>(RetryHistory.MakeId()) ?? RetryHistory.CreateNew();

                retryHistory.AddToHistory(new HistoricRetryOperation
                {
                    RequestId = message.RequestId,
                    RetryType = message.RetryType,
                    StartTime = message.StartTime,
                    CompletionTime = message.CompletionTime,
                    Originator = message.Originator,
                    Failed = message.Failed,
                    NumberOfMessagesProcessed = message.NumberOfMessagesProcessed
                }, Settings.RetryHistoryDepth);

                retryHistory.AddToUnacknowledged(new UnacknowledgedRetryOperation
                {
                    RequestId = message.RequestId,
                    RetryType = message.RetryType,
                    StartTime = message.StartTime,
                    CompletionTime = message.CompletionTime,
                    Originator = message.Originator,
                    Classifier = message.Classifier,
                    Failed = message.Failed,
                    NumberOfMessagesProcessed = message.NumberOfMessagesProcessed,
                    Last = message.Last,
                });

                session.Store(retryHistory);
                session.SaveChanges();
            }
        }
    }
}