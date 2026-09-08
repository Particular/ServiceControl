namespace ServiceControl.Retention.Api;

using System;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.WebApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;
using ServiceControl.Infrastructure.Auth;

// Manual retention-purge endpoint. Lives only on the primary error instance (the sweeper is only
// registered there). On a RavenDB-backed instance IRetentionSweeper is not registered, so the
// IRetentionApi implementation returns a "not-supported" status that this controller maps to 501.
[ApiController]
[Route("api/maintenance")]
public class SystemMaintenanceController(IRetentionApi retentionApi, ICurrentUserAccessor userAccessor, IMessageActionAuditLog auditLog) : ControllerBase
{
    // Starts a full retention purge with caller-supplied cutoffs. The delete work runs in the
    // background on a host-lifetime token; this returns as soon as the run is accepted (202),
    // already running (409), unsupported by the persister (501), or the cutoff was invalid (400).
    [Authorize(Policy = Permissions.ErrorRetentionPurge)]
    [Route("retention/purge")]
    [HttpPost]
    public async Task<IActionResult> Purge([FromBody] RetentionPurgeRequest request, CancellationToken cancellationToken = default)
    {
        var user = userAccessor.Resolve(User);
        var operationId = this.AuditOperationId();
        RetentionPurgeResponse response = null;
        return await auditLog.AuditedOperation(user, MessageActionKind.Delete, Permissions.ErrorRetentionPurge, MessageActionScope.Range, null, null, operationId, async ct =>
        {
            response = await retentionApi.Sweep(request ?? new RetentionPurgeRequest(), ct);
            return ToActionResult(response);
        }, cancellationToken);

        IActionResult ToActionResult(RetentionPurgeResponse retentionPurgeResponse) =>
            retentionPurgeResponse.Status switch
            {
                RetentionPurgeStatus.Started =>Accepted(retentionPurgeResponse),
                RetentionPurgeStatus.AlreadyRunning => Conflict(retentionPurgeResponse),
                RetentionPurgeStatus.NotSupported => StatusCode(501, retentionPurgeResponse),
                RetentionPurgeStatus.Error =>BadRequest(retentionPurgeResponse),
                _ => throw new ArgumentOutOfRangeException(nameof(retentionPurgeResponse.Status), retentionPurgeResponse.Status, "Unexpected retention purge status.")
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