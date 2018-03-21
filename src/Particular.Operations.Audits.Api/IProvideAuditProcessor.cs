namespace Particular.Operations.Audits.Api
{
    public interface IProvideAuditProcessor
    {
        IProcessAudits ProcessAudits { get; }
    }
}