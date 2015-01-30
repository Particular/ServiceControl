namespace Particular.Operations.Ingestion.Api
{
    public interface IProcessFailedMessages
    {
        void ProcessFailed(IngestedMessage message);
    }
}