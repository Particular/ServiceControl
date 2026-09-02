namespace ServiceControl.Retention.Api;

using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;

// Manual retention-sweep endpoint. Lives only on the primary error instance (the sweeper is only
// registered there). On a RavenDB-backed instance IRetentionSweeper is not registered, so the
// IRetentionApi implementation returns a "not-supported" status that this controller maps to 501.
[ApiController]
[Route("api")]
public class RetentionController(IRetentionApi retentionApi) : ControllerBase
{
    // Starts a full retention sweep with caller-supplied cutoffs. The delete work runs in the
    // background on a host-lifetime token; this returns as soon as the run is accepted (202),
    // already running (409), in maintenance mode (503), unsupported by the persister (501), or
    // the cutoff was invalid (400).
    [Authorize(Policy = Permissions.ErrorRetentionSweep)]
    [Route("retention/sweep")]
    [HttpPost]
    public async Task<IActionResult> Sweep([FromBody] RetentionSweepRequest request, CancellationToken cancellationToken = default)
    {
        var response = await retentionApi.SweepAsync(request ?? new RetentionSweepRequest(), cancellationToken);

        return response.Status switch
        {
            "started" => Accepted(response),
            "already-running" => Conflict(response),
            "maintenance" => StatusCode(503, response),
            "not-supported" => StatusCode(501, response),
            "invalid-cutoff" => BadRequest(response),
            _ => Ok(response)
        };
    }

    // Polls the execution state of the most recent sweep.
    [Authorize(Policy = Permissions.ErrorRetentionSweep)]
    [Route("retention/sweep/status")]
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken cancellationToken = default)
    {
        var status = await retentionApi.GetStatusAsync(cancellationToken);

        // A reason is present only when the persister has no sweeper (e.g. RavenDB).
        return status.Reason is not null ? StatusCode(501, status) : Ok(status);
    }
}