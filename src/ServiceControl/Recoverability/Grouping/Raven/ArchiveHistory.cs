namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;

    public class ArchiveHistory
    {
        public string Id { get; set; }
        public List<HistoricArchiveOperation> HistoricOperations { get; set; }
        public List<UnacknowledgedOperation> UnacknowledgedOperations { get; set; }

        public static string MakeId()
        {
            return "ArchiveOperations/History";
        }

        public static ArchiveHistory CreateNew()
        {
            return new ArchiveHistory
            {
                HistoricOperations = new List<HistoricArchiveOperation>(),
                UnacknowledgedOperations = new List<UnacknowledgedOperation>(),
                Id = MakeId()
            };
        }

        public void AddToHistory(HistoricArchiveOperation historicOperation, int historyDepth)
        {
            HistoricOperations = HistoricOperations.Union(new[]
                {
                    historicOperation
                })
                .OrderByDescending(archive => archive.CompletionTime)
                .Take(historyDepth)
                .ToList();
        }

        public string GetHistoryOperationsUniqueIdentifier()
        {
            return string.Join(",", HistoricOperations.Select(x => x.RequestId));
        }

        public void AddToUnacknowledged(UnacknowledgedOperation unacknowledgedArchiveOperation)
        {
            UnacknowledgedOperations.Add(unacknowledgedArchiveOperation);
        }

        public UnacknowledgedOperation[] GetUnacknowledgedByClassifier(string classifier)
        {
            return UnacknowledgedOperations.Where(x => x.Classifier == classifier).ToArray();
        }

        public void Acknowledge(string requestId, ArchiveType type)
        {
            UnacknowledgedOperations.RemoveAll(x => x.RequestId == requestId && (ArchiveType)x.OperationType == type);
        }
    }
}