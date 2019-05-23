namespace ServiceControl.Recoverability
{
    public struct RetryProgress
    {
        public RetryProgress(double percentage, int messagesPrepared, int messagesForwarded, int messagesSkipped, int messagesRemaining)
        {
            Percentage = percentage;
            MessagesPrepared = messagesPrepared;
            MessagesForwarded = messagesForwarded;
            MessagesSkipped = messagesSkipped;
            MessagesRemaining = messagesRemaining;
        }

        public double Percentage { get; set; }
        public int MessagesPrepared { get; set; }
        public int MessagesForwarded { get; set; }
        public int MessagesSkipped { get; set; }
        public int MessagesRemaining { get; set; }
    }
}