namespace ServiceControl.Recoverability.API
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence.Recoverability;

    [ApiController]
    [Route("api")]
    public class FailureGroupsUnarchiveController(
        IMessageSession bus,
        IArchiveMessages archiver,
        ICurrentUserAccessor userAccessor,
        IMessageActionAuditLog auditLog) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsUnarchive)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors/unarchive")]
        [HttpPost]
        public async Task<IActionResult> UnarchiveGroupErrors(string groupId)
        {
            auditLog.Operation(userAccessor.Resolve(User), MessageActionKind.Unarchive,
                Permissions.ErrorRecoverabilityGroupsUnarchive, MessageActionScope.Group,
                resource: groupId, count: null, operationId: Guid.NewGuid().ToString("N"));

            if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await archiver.StartUnarchiving(groupId, ArchiveType.FailureGroup);

                await bus.SendLocal<UnarchiveAllInGroup>(m => { m.GroupId = groupId; });
            }

            return Accepted();
        }
    }
}