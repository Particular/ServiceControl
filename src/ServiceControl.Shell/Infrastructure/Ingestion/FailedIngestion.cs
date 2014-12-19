namespace ServiceControl.Shell.Infrastructure.Ingestion
{
    using System;
    using NServiceBus;

    public class FailedIngestion
    {
        public Guid Id { get; set; }
        public TransportMessage Message { get; set; }
        public string SourceQueue { get; set; }
    }
}