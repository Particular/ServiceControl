namespace ServiceControl.Recoverability
{
    public static class OperationProgressCalculator
    {
        public static double CalculateProgress(int totalNumberOfMessages, int numberOfMessagesPrepared, int numberOfMessagesForwarded, int numberOfMessagesSkipped, RetryState state)
        {
            double total = totalNumberOfMessages;

            return state switch
            {
                RetryState.Preparing => numberOfMessagesPrepared / total,
                RetryState.Forwarding => (numberOfMessagesForwarded + numberOfMessagesSkipped) / total,
                RetryState.Completed => 1.0,
                RetryState.Waiting or _ => 0.0,
            };
        }

        public static double CalculateProgress(int totalNumberOfMessages, int numberOfMessagesArchived, ArchiveState state)
        {
            double total = totalNumberOfMessages;

            return state switch
            {
                ArchiveState.ArchiveProgressing => numberOfMessagesArchived / total,
                ArchiveState.ArchiveFinalizing or ArchiveState.ArchiveCompleted => 1.0,
                ArchiveState.ArchiveStarted or _ => 0.0,
            };
        }
    }
}