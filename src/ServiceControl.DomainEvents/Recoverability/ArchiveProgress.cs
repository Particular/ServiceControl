namespace ServiceControl.Recoverability
{
    public struct ArchiveProgress
    {
        public ArchiveProgress(double roundedPercentage, int totalNumberOfMessages, int numberOfMessagesArchived, int remaining)
        {
            Percentage = roundedPercentage;
            TotalNumberOfMessages = totalNumberOfMessages;
            NumberOfMessagesArchived = numberOfMessagesArchived;
            MessagesRemaining = remaining;
        }

        public double Percentage { get; set; }
        public int TotalNumberOfMessages { get; set; }
        public int NumberOfMessagesArchived { get; set; }
        public int MessagesRemaining { get; set; }
    }
}