namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using NServiceBus;

    public class EditAndSend : ICommand
    {
        public string FailedMessageId { get; set; }

        public string NewBody { get; set; }

        public Dictionary<string, string> NewHeaders { get; set; }
    }
}