namespace ServiceControl.Operations
{
    using Contracts.Operations;

    public abstract class ImportEnricher : IEnrichImportedMessages
    {
        public abstract void Enrich(ImportMessage message);
    }
}