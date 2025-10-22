namespace ServiceControl.Monitoring;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Persistence;
using ServiceBus.Management.Infrastructure.Settings;

public class EndpointSettingsUpdateModel
{
    public bool TrackInstances { get; set; }
}

public class SettingsData
{
    public string Name { get; set; }
    public bool TrackInstances { get; set; }
}

[ApiController]
[Route("api")]
public class EndpointsSettingsController(
    IEndpointSettingsStore dataStore, Settings settings)
    : ControllerBase
{
    [Route("endpointssettings")]
    [HttpGet]
    public async IAsyncEnumerable<SettingsData> Endpoints([EnumeratorCancellation] CancellationToken token)
    {
        await using IAsyncEnumerator<EndpointSettings> enumerator =
            dataStore.GetAllEndpointSettings().GetAsyncEnumerator(token);
        bool noResults = true;
        while (await enumerator.MoveNextAsync())
        {
            noResults = false;
            yield return new SettingsData
            {
#pragma warning disable IDE0055
                Name = enumerator.Current.Name, TrackInstances = enumerator.Current.TrackInstances
#pragma warning restore IDE0055
            };
        }

        if (noResults)
        {
            yield return new SettingsData { Name = string.Empty, TrackInstances = settings.ServiceControl.TrackInstancesInitialValue };
        }
    }

    [Route("endpointssettings/{endpointName?}")]
    [HttpPatch]
    public async Task<IActionResult>
        UpdateTrackingSetting(string endpointName, [FromBody] EndpointSettingsUpdateModel data, CancellationToken token)
    {
        await dataStore.UpdateEndpointSettings(new EndpointSettings
        {
#pragma warning disable IDE0055
            Name = endpointName ?? string.Empty, TrackInstances = data.TrackInstances
#pragma warning restore IDE0055
        }, token);
        return Accepted();
    }
}