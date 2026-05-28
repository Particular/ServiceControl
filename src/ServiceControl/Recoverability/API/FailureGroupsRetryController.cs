namespace ServiceControl.Recoverability.API
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Infrastructure.Auth.Rbac;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Infrastructure.WebApi.Auth;
    using ServiceControl.Persistence;

    [ApiController]
    [Route("api")]
    public class FailureGroupsRetryController(IMessageSession bus, RetryingManager retryingManager, IPermissionEvaluator permissionEvaluator) : ControllerBase
    {
        [RequirePermission(Permissions.RecoverabilityGroupsRetry)]
        [Authorize(Policy = Permissions.RecoverabilityGroupsRetry)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors/retry")]
        [HttpPost]
        public async Task<IActionResult> ArchiveGroupErrors(string groupId)
        {
            // S2 fail-closed for group operations: groups span multiple queues and cannot be
            // cleanly scope-checked against a single queue address. An unrestricted grant
            // (no scope restriction) is required to operate on groups.
            //
            // v1 documented limitation: scoped users must use per-message retry operations.
            // Unrestricted users (sc-admin with "*" or sc-operator with no scope) proceed normally.
            if (!permissionEvaluator.HasUnrestrictedGrant(User, Permissions.RecoverabilityGroupsRetry))
            {
                Response.ContentType = "application/json";
                Response.StatusCode = StatusCodes.Status403Forbidden;
                await Response.WriteAsJsonAsync(new
                {
                    error = "forbidden",
                    permission = Permissions.RecoverabilityGroupsRetry,
                    resource = groupId,
                    reason = $"Group '{groupId}' cannot be scope-verified — access denied fail-closed for scoped users. Use per-message retry operations."
                });
                return Empty;
            }

            var started = DateTime.UtcNow;

            if (!retryingManager.IsOperationInProgressFor(groupId, RetryType.FailureGroup))
            {
                await retryingManager.Wait(groupId, RetryType.FailureGroup, started);

                await bus.SendLocal(new RetryAllInGroup
                {
                    GroupId = groupId,
                    Started = started
                });
            }

            return Accepted();
        }
    }
}
