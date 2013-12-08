namespace ServiceControl.MessageFailures.InternalMessages
{
    using System.Collections.Generic;
    using NServiceBus;

    public class RequestRetries : ICommand
    {
        public List<string> MessageIds { get; set; }
    }
}