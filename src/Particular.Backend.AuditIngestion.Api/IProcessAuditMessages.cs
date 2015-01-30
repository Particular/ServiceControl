namespace Particular.Backend.AuditIngestion.Api
{
    public interface IProcessAuditMessages
    {
        void Process(IngestedAuditMessage message);
    }
}