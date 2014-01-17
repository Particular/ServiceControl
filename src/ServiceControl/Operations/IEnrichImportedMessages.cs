namespace ServiceControl.Operations
{
    using Contracts.Operations;

    interface IEnrichImportedMessages
    {
        void Enrich(ImportMessage message);

    }
}