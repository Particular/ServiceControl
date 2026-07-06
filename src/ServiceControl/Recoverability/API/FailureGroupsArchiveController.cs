namespace ServiceControl.Recoverability.API
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
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
            if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                var user = userAccessor.Resolve(User);
                var operationId = this.AuditOperationId();
                auditLog.Operation(user, MessageActionKind.Archive,
                    Permissions.ErrorRecoverabilityGroupsArchive, MessageActionScope.Group,
                    resource: groupId, count: null, operationId: operationId);

                await archiver.StartArchiving(groupId, ArchiveType.FailureGroup);

                var sendOptions = new SendOptions();
                sendOptions.RouteToThisEndpoint();
                AuditHeaders.Stamp(sendOptions, user, operationId);

                await bus.Send<ArchiveAllInGroup>(m => { m.GroupId = groupId; }, sendOptions);
            }

            return Accepted();
        }
    }
}