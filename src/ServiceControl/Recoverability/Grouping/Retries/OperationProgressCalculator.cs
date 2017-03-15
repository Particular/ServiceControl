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

        public static double CalculateProgress(int totalNumberOfMessages, int numberOfMessagesPrepared, int numberOfMessagesForwarded, int numberOfMessagesSkipped, ArchiveState state)
        {
            double total = totalNumberOfMessages;

            switch (state)
            {
                case ArchiveState.Archiving:
                    return (numberOfMessagesForwarded + numberOfMessagesSkipped) / total;
                case ArchiveState.Completed:
                    return 1.0;
                default:
                    return 0.0;
            }
        }
    }
}