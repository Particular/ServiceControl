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
            if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                var user = userAccessor.Resolve(User);
                var operationId = this.AuditOperationId();
                auditLog.Operation(user, MessageActionKind.Unarchive,
                    Permissions.ErrorRecoverabilityGroupsUnarchive, MessageActionScope.Group,
                    resource: groupId, count: null, operationId: operationId);

                await archiver.StartUnarchiving(groupId, ArchiveType.FailureGroup);

                var sendOptions = new SendOptions();
                sendOptions.RouteToThisEndpoint();
                AuditHeaders.Stamp(sendOptions, user, operationId);

                await bus.Send<UnarchiveAllInGroup>(m => { m.GroupId = groupId; }, sendOptions);
            }

            return Accepted();
        }
    }
}