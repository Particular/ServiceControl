namespace ServiceControl.Shell.Api.Ingestion
{
    public interface IProcessIngestedMessages
    {
        void Process(IngestedMessage message);
    }
}