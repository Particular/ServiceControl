namespace ServiceControl.MessageFailures
{
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.Operations;

    public interface IEnrichMessageFailureHistory
    {
        void Enrich(MessageFailureHistory history, IngestedMessage actualMessage, FailureDetails failureDetails);
    }
}