namespace Particular.Operations.Ingestion.Api
{
    public interface IProcessSuccessfulMessages
    {
        void ProcessSuccessful(IngestedMessage message);
    }
}