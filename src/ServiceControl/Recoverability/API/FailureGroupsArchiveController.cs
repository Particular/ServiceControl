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
    public class FailureGroupsArchiveController(
        IMessageSession bus,
        IArchiveMessages archiver,
        ICurrentUserAccessor userAccessor,
        IMessageActionAuditLog auditLog) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsArchive)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors/archive")]
        [HttpPost]
        public async Task<IActionResult> ArchiveGroupErrors(string groupId)
        {
            auditLog.Operation(userAccessor.Resolve(User), MessageActionKind.Archive,
                Permissions.ErrorRecoverabilityGroupsArchive, MessageActionScope.Group,
                resource: groupId, count: null, operationId: Guid.NewGuid().ToString("N"));

            if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await archiver.StartArchiving(groupId, ArchiveType.FailureGroup);

                await bus.SendLocal<ArchiveAllInGroup>(m => { m.GroupId = groupId; });
            }

            return Accepted();
        }
    }
}