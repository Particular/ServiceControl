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
    public class FailureGroupsArchiveController(IMessageSession bus, IArchiveMessages archiver, IPermissionEvaluator permissionEvaluator) : ControllerBase
    {
        [RequirePermission(Permissions.RecoverabilityGroupsArchive)]
        [Authorize(Policy = Permissions.RecoverabilityGroupsArchive)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors/archive")]
        [HttpPost]
        public async Task<IActionResult> ArchiveGroupErrors(string groupId)
        {
            // S2 fail-closed for group operations: groups span multiple queues and cannot be
            // cleanly scope-checked against a single queue address. An unrestricted grant
            // (no scope restriction) is required to operate on groups.
            //
            // v1 documented limitation: scoped users must use per-message archive operations.
            if (!permissionEvaluator.HasUnrestrictedGrant(User, Permissions.RecoverabilityGroupsArchive))
            {
                Response.ContentType = "application/json";
                Response.StatusCode = StatusCodes.Status403Forbidden;
                await Response.WriteAsJsonAsync(new
                {
                    error = "forbidden",
                    permission = Permissions.RecoverabilityGroupsArchive,
                    resource = groupId,
                    reason = $"Group '{groupId}' cannot be scope-verified — access denied fail-closed for scoped users. Use per-message archive operations."
                });
                return Empty;
            }

            if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await archiver.StartArchiving(groupId, ArchiveType.FailureGroup);

                await bus.SendLocal<ArchiveAllInGroup>(m => { m.GroupId = groupId; });
            }

            return Accepted();
        }
    }
}
