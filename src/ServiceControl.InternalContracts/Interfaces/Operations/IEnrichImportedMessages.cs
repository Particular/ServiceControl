namespace ServiceControl.Operations
{
    using Contracts.Operations;

    public interface IEnrichImportedMessages
    {
        void Enrich(ImportMessage message);

    }
}