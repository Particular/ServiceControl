#nullable enable
namespace ServiceControl.Persistence.RavenDB;

using System.Threading.Tasks;

sealed class RavenExternalConnectionStringProvider(RavenPersisterSettings settings)
    : IConnectionStringProvider
{
    public Task<string> GetConnectionString() => Task.FromResult(settings.ConnectionString);
}