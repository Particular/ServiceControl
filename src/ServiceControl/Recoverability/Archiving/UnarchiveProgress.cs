// unset

namespace ServiceControl.Recoverability
{
    public struct UnarchiveProgress
    {
        public UnarchiveProgress(double roundedPercentage, int totalNumberOfMessages, int numberOfMessagesUnarchived, int remaining)
        {
            Percentage = roundedPercentage;
            TotalNumberOfMessages = totalNumberOfMessages;
            NumberOfMessagesUnarchived = numberOfMessagesUnarchived;
            MessagesRemaining = remaining;
        }

        public double Percentage { get; set; }
        public int TotalNumberOfMessages { get; set; }
        public int NumberOfMessagesUnarchived { get; set; }
        public int MessagesRemaining { get; set; }
    }
}