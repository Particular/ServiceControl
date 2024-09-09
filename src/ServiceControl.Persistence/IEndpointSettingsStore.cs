namespace ServiceControl.Persistence;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IEndpointSettingsStore
{
    IAsyncEnumerable<EndpointSettings> GetAllEndpointSettings();

    Task UpdateEndpointSettings(EndpointSettings settings, CancellationToken token);
    Task Delete(string name, CancellationToken cancellationToken);
}