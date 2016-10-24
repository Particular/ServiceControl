namespace ServiceControl.Recoverability
{
    public static class RetryOperationProgressionCalculator
    {
        public static double CalculateProgression(RetryOperationSummary retry)
        {
            const double waitingWeight = 0.05;
            const double prepairedWeight = 0.475;
            const double forwardedWeight = 0.475;

            if (retry.RetryState == RetryState.Waiting)
            {
                return waitingWeight;
            }

            if (retry.RetryState == RetryState.Completed)
            {
                return 1.0;
            }

            double total = retry.TotalNumberOfMessages;
            double preparedPercentage = retry.NumberOfMessagesPrepared / total;
            double forwardedPercentage = retry.NumberOfMessagesForwarded / total;

            return waitingWeight + preparedPercentage * prepairedWeight + forwardedPercentage * forwardedWeight;
        }
    }
}