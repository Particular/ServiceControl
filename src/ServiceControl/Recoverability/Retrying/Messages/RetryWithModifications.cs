namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using NServiceBus;

    public class RetryWithModifications : ICommand
    {
        public string FailedMessageId { get; set; }

        public string NewBody { get; set; }

        public Dictionary<string, string> NewHeaders { get; set; }
    }
}