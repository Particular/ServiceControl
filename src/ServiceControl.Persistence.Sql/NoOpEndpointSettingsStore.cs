namespace ServiceControl.Persistence.Sql;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Operations;
using ServiceControl.Persistence;

class NoOpEndpointSettingsStore : IEndpointSettingsStore
{
    public async IAsyncEnumerable<EndpointSettings> GetAllEndpointSettings()
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task UpdateEndpointSettings(EndpointSettings settings, CancellationToken token) => Task.CompletedTask;

    public Task Delete(string name, CancellationToken cancellationToken) => Task.CompletedTask;
}
