namespace ServiceControl.Recoverability.Groups.Retry
{
    using NServiceBus;
    using NServiceBus.IdGeneration;

    public class RetryAllInGroup : ICommand
    {
        public string BatchId { get; set; }
        public string GroupId { get; set; }

        public RetryAllInGroup()
        {
            BatchId = CombGuid.Generate().ToString();
        }
    }
}