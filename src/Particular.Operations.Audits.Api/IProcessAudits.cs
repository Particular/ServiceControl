using System.Threading.Tasks;

namespace Particular.Operations.Audits.Api
{
    public interface IProcessAudits
    {
        Task Handle(AuditMessage message);
    }
}
