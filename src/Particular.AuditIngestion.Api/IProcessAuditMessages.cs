namespace Particular.AuditIngestion.Api
{
    public interface IProcessAuditMessages
    {
        void Process(IngestedAuditMessage message);
    }
}