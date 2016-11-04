namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using System.Linq;

    public class RetryOperationCompletedHandler : IHandleMessages<RetryOperationCompleted>
    {
        public Settings Settings { get; set; }
        public IDocumentStore Store { get; set; }
        public RetryOperationManager RetryOperationManager { get; set; }

        public void Handle(RetryOperationCompleted message)
        {
            if (message.Failed)
            {
                return;
            }

            var completedOperation = new CompletedRetryOperation
            {
                RequestId = message.RequestId,
                RetryType = message.RetryType,
                CompletionTime = message.CompletionTime,
                Originator = message.Originator,
            };

            using (var session = Store.OpenSession())
            {
                var retryHistory = session.Load<RetryOperationsHistory>(RetryOperationsHistory.MakeId()) ?? RetryOperationsHistory.CreateNew();

                retryHistory.PreviousFullyCompletedOperations = retryHistory.PreviousFullyCompletedOperations.Union(new[] { completedOperation })
                    .OrderByDescending(retry => retry.CompletionTime)
                    .Take(Settings.RetryHistoryDepth)
                    .ToArray();

                session.Store(retryHistory);
                session.SaveChanges();
            }
        }
    }
}