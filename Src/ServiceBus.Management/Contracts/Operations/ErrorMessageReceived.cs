namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;
    using NServiceBus;

    public class ErrorMessageReceived : IEvent
    {
        public string Id { get; set; }
        public byte[] Body { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionSource { get; set; }
        public string ExceptionStackTrace { get; set; }
        public string ExceptionType { get; set; }
        public IDictionary<string, string> Headers { get; set; }
        public string ReplyToAddress { get; set; }
    }
}
