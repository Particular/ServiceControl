namespace ServiceControl.Recoverability
{
    public static class RetryOperationProgressionCalculator
    {
        public static double CalculateProgression(int totalNumberOfMessages, int numberOfMessagesPrepared, int numberOfMessagesForwarded)
        {
            const double waitingWeight = 0.05;
            const double prepairedWeight = 0.475;
            const double forwardedWeight = 0.475;

            if (totalNumberOfMessages == 0)
            {
                return waitingWeight;
            }

            double total = totalNumberOfMessages;
            double preparedPercentage = numberOfMessagesPrepared / total;
            double forwardedPercentage = numberOfMessagesForwarded / total;

            return waitingWeight + preparedPercentage * prepairedWeight + forwardedPercentage * forwardedWeight;
        }
    }
}