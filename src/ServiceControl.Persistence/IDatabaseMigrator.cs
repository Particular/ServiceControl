namespace ServiceControl.Persistence;

using System.Threading;
using System.Threading.Tasks;

public interface IDatabaseMigrator
{
    Task ApplyMigrations(CancellationToken cancellationToken = default);
}
