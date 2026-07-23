namespace ServiceControl.Persistence;

using System.Threading;
using System.Threading.Tasks;

public interface IBodyStorageInstaller
{
    Task Provision(CancellationToken cancellationToken = default);
}
