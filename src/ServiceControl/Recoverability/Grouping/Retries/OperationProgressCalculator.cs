namespace ServiceControl.Recoverability
{
    public static class OperationProgressCalculator
    {
        public static double CalculateProgress(int totalNumberOfMessages, int numberOfMessagesPrepared, int numberOfMessagesForwarded, int numberOfMessagesSkipped, RetryState state)
        {
            double total = totalNumberOfMessages;

            switch (state)
            {
                case RetryState.Preparing:
                    return numberOfMessagesPrepared / total;
                case RetryState.Forwarding:
                    return (numberOfMessagesForwarded + numberOfMessagesSkipped) / total;
                case RetryState.Completed:
                    return 1.0;
                default:
                    return 0.0;
            }
        }

        public static double CalculateProgress(int totalNumberOfMessages, int numberOfMessagesArchived, ArchiveState state)
        {
            double total = totalNumberOfMessages;

            switch (state)
            {
                case ArchiveState.ArchiveProgressing:
                    return numberOfMessagesArchived / total;
                case ArchiveState.ArchiveCompleted:
                    return 1.0;
                default:
                    return 0.0;
            }
        }
    }
}