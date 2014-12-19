namespace ServiceControl.Shell.Api.Ingestion
{
    public abstract class MessageIngestor
    {
        public abstract string Address { get; }
        public abstract void Process(IngestedMessage message);
    }
}