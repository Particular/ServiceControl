namespace ServiceControl.Monitoring;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Policy = Permissions.ErrorEndpointsView)]
    [Route("endpointssettings")]
    [HttpGet]
    public async IAsyncEnumerable<SettingsData> Endpoints([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using IAsyncEnumerator<EndpointSettings> enumerator =
            dataStore.GetAllEndpointSettings(cancellationToken).GetAsyncEnumerator(cancellationToken);
        bool hasDefault = false;
        while (await enumerator.MoveNextAsync())
        {
            hasDefault |= enumerator.Current.Name == string.Empty;
            yield return new SettingsData
            {
                Name = enumerator.Current.Name,
                TrackInstances = enumerator.Current.TrackInstances
            };
        }

        if (!hasDefault)
        {
            yield return new SettingsData { Name = string.Empty, TrackInstances = settings.TrackInstancesInitialValue };
        }
    }

    [Authorize(Policy = Permissions.ErrorEndpointsManage)]
    [Route("endpointssettings/{endpointName?}")]
    [HttpPatch]
    public async Task<IActionResult>
        UpdateTrackingSetting(string endpointName, [FromBody] EndpointSettingsUpdateModel data, CancellationToken cancellationToken = default)
    {
        await dataStore.UpdateEndpointSettings(new EndpointSettings
        {
#pragma warning disable IDE0055
            Name = endpointName ?? string.Empty, TrackInstances = data.TrackInstances
#pragma warning restore IDE0055
        }, cancellationToken);
        return Accepted();
    }
}