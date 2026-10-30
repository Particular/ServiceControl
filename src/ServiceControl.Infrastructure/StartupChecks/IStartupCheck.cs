namespace ServiceControl.Infrastructure.StartupChecks;

using System.Threading;
using System.Threading.Tasks;

public interface IStartupCheck
{
    Task Check(CancellationToken cancellationToken = default);
}
