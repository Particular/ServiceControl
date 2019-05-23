namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class RetryMessagesById : ICommand
    {
        public string[] MessageUniqueIds { get; set; }
    }
}