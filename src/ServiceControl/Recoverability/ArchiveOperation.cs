namespace ServiceControl.Recoverability
{
    using System;

    public class ArchiveOperation
    {
        public int TotalNumberOfMessages { get; private set; }
        public int NumberOfMessagesPrepared { get; private set; }
        public int NumberOfMessagesForwarded { get; private set; }
        public int NumberOfMessagesSkipped { get; set; }
        public DateTime? CompletionTime { get; private set; }
        public DateTime? Last { get; private set; }
        public DateTime Started { get; private set; }
        public ArchiveState ArchiveState { get; private set; }

        public static string MakeOperationId(string requestId, ArchiveType archiveType)
        {
            return $"{archiveType}/{requestId}";
        }

        public Progress GetProgress()
        {
            var percentage = OperationProgressCalculator.CalculateProgress(TotalNumberOfMessages, NumberOfMessagesPrepared, NumberOfMessagesForwarded, NumberOfMessagesSkipped, ArchiveState);
            var roundedPercentage = Math.Round(percentage, 2);

            var remaining = TotalNumberOfMessages - (NumberOfMessagesForwarded + NumberOfMessagesSkipped);

            return new Progress(roundedPercentage, NumberOfMessagesPrepared, NumberOfMessagesForwarded, NumberOfMessagesSkipped, remaining);
        }
    }
}