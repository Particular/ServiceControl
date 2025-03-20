namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.Persistence;

    public class RetryHistory
    {
        public string Id { get; set; }
        public List<HistoricRetryOperation> HistoricOperations { get; set; }
        public List<UnacknowledgedRetryOperation> UnacknowledgedOperations { get; set; }

        public static string MakeId()
        {
            return "RetryOperations/History";
        }

        public static RetryHistory CreateNew()
        {
            return new RetryHistory
            {
                HistoricOperations = [],
                UnacknowledgedOperations = [],
                Id = MakeId()
            };
        }

        public void AddToHistory(HistoricRetryOperation historicOperation, int historyDepth)
        {
            HistoricOperations = HistoricOperations.Union(new[]
                {
                    historicOperation
                })
                .OrderByDescending(retry => retry.CompletionTime)
                .Take(historyDepth)
                .ToList();
        }

        public string GetHistoryOperationsUniqueIdentifier()
        {
            return string.Join(',', HistoricOperations.Select(x => x.RequestId));
        }

        public void AddToUnacknowledged(UnacknowledgedRetryOperation unacknowledgedRetryOperation)
        {
            UnacknowledgedOperations.Add(unacknowledgedRetryOperation);

            UnacknowledgedOperations = UnacknowledgedOperations
                // All other retry types already have an explicit way to dismiss them on the UI
                .Where(operation => operation.RetryType is not RetryType.MultipleMessages and not RetryType.SingleMessage)
                .ToList();
        }

        public UnacknowledgedRetryOperation[] GetUnacknowledgedByClassifier(string classifier)
        {
            return UnacknowledgedOperations.Where(x => x.Classifier == classifier).ToArray();
        }

        public bool Acknowledge(string requestId, RetryType type)
        {
            return UnacknowledgedOperations.RemoveAll(x => x.RequestId == requestId && x.RetryType == type) > 0;
        }
    }
}