namespace ServiceControl.Persistence.EFCore.Implementation;

public class EndpointSettingsStore : IEndpointSettingsStore
{
    public IAsyncEnumerable<EndpointSettings> GetAllEndpointSettings(CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task UpdateEndpointSettings(EndpointSettings settings, CancellationToken token) =>
        throw new NotImplementedException();

    public Task Delete(string name, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
