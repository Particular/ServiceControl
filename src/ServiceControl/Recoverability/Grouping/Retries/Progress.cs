namespace ServiceControl.Recoverability
{
    public struct Progress
    {
        public Progress(double percentage, int messagesPrepared, int messagesForwarded, int messagesSkipped, int messagesRemaining)
        {
            Percentage = percentage;
            MessagesPrepared = messagesPrepared;
            MessagesForwarded = messagesForwarded;
            MessagesSkipped = messagesSkipped;
            MessagesRemaining = messagesRemaining;
        }

        public double Percentage { get; private set; }
        public int MessagesPrepared { get; private set; }
        public int MessagesForwarded { get; private set; }
        public int MessagesSkipped { get; private set; }
        public int MessagesRemaining { get; private set; }
    }
}