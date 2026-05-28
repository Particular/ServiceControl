namespace ServiceControl.Recoverability.API
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Infrastructure.Auth.Rbac;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Infrastructure.WebApi.Auth;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Recoverability;

    [ApiController]
    [Route("api")]
    public class FailureGroupsUnarchiveController(IMessageSession bus, IArchiveMessages archiver, IPermissionEvaluator permissionEvaluator) : ControllerBase
    {
        [Authorize(Policy = Permissions.RecoverabilityGroupsUnarchive)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors/unarchive")]
        [HttpPost]
        public async Task<IActionResult> UnarchiveGroupErrors(string groupId)
        {
            // S2 fail-closed for group operations: groups span multiple queues and cannot be
            // cleanly scope-checked against a single queue address. An unrestricted grant
            // (no scope restriction) is required to operate on groups.
            //
            // v1 documented limitation: scoped users must use per-message unarchive operations.
            if (!permissionEvaluator.HasUnrestrictedGrant(User, Permissions.RecoverabilityGroupsUnarchive))
            {
                Response.ContentType = "application/json";
                Response.StatusCode = StatusCodes.Status403Forbidden;
                await Response.WriteAsJsonAsync(new
                {
                    error = "forbidden",
                    permission = Permissions.RecoverabilityGroupsUnarchive,
                    resource = groupId,
                    reason = $"Group '{groupId}' cannot be scope-verified — access denied fail-closed for scoped users. Use per-message unarchive operations."
                });
                return Empty;
            }

            if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await archiver.StartUnarchiving(groupId, ArchiveType.FailureGroup);

                await bus.SendLocal<UnarchiveAllInGroup>(m => { m.GroupId = groupId; });
            }

            return Accepted();
        }
    }
}
