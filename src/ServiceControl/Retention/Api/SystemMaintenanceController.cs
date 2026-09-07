namespace ServiceControl.Retention.Api;

using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Api;
using Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;

// Manual retention-purge endpoint. Lives only on the primary error instance (the sweeper is only
// registered there). On a RavenDB-backed instance IRetentionSweeper is not registered, so the
// IRetentionApi implementation returns a "not-supported" status that this controller maps to 501.
[ApiController]
[Route("api/maintenance")]
public class SystemMaintenanceController(IRetentionApi retentionApi) : ControllerBase
{
    // Starts a full retention purge with caller-supplied cutoffs. The delete work runs in the
    // background on a host-lifetime token; this returns as soon as the run is accepted (202),
    // already running (409), in maintenance mode (503), unsupported by the persister (501), or
    // the cutoff was invalid (400).
    [Authorize(Policy = Permissions.ErrorRetentionPurge)]
    [Route("retention/purge")]
    [HttpPost]
    public async Task<IActionResult> Purge([FromBody] RetentionPurgeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await retentionApi.Sweep(request ?? new RetentionPurgeRequest(), cancellationToken);

        return response.Status switch
        {
            RetentionApi.StatusStarted => Accepted(response),
            RetentionApi.StatusAlreadyRunning => Conflict(response),
            RetentionApi.StatusMaintenance => StatusCode(503, response),
            RetentionApi.StatusNotSupported => StatusCode(501, response),
            RetentionApi.StatusInvalidCutoff => BadRequest(response),
            _ => Ok(response)
        };
    }

    // Polls the execution state of the most recent sweep/purge operation.
    [Authorize(Policy = Permissions.ErrorRetentionPurge)]
    [Route("retention/purge/status")]
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken cancellationToken = default)
    {
        var status = await retentionApi.GetStatus(cancellationToken);

        // A reason is present only when the persister has no sweeper (e.g. RavenDB).
        return status.Reason is not null ? StatusCode(501, status) : Ok(status);
    }
}