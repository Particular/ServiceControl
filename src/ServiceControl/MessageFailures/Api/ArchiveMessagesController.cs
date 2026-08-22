namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using InternalMessages;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;

    [ApiController]
    [Route("api")]
    public class ArchiveMessagesController(IMessageSession messageSession, IGroupsDataStore dataStore, ICurrentUserAccessor userAccessor, IMessageActionAuditLog auditLog) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorMessagesArchive)]
        [Route("errors/archive")]
        [HttpPost]
        [HttpPatch]
        public async Task<IActionResult> ArchiveBatch(string[] messageIds, CancellationToken cancellationToken = default)
        {
            if (messageIds.Any(string.IsNullOrEmpty))
            {
                ModelState.AddModelError(nameof(messageIds), "Cannot contain null or empty message IDs.");
                return UnprocessableEntity(ModelState);
            }

            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Archive, Permissions.ErrorMessagesArchive, MessageActionScope.Batch,
                resource: null, count: messageIds.Length, operationId: operationId, async ct =>
                {
                    foreach (var id in messageIds)
                    {
                        await messageSession.Send(new ArchiveMessage { FailedMessageId = id, Scope = MessageActionScope.Batch }, AuditHeaders.LocalSendOptions(user, operationId), ct);
                    }
                }, cancellationToken);

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorMessagesView)]
        [Route("errors/groups/{classifier?}")]
        [HttpGet]
        public async Task<IActionResult> GetArchiveMessageGroups(string classifier = "Exception Type and Stack Trace", CancellationToken cancellationToken = default)
        {
            var result = await dataStore.GetArchivedGroupsByClassifier(classifier, cancellationToken);

            Response.WithEtag(result.QueryStats.Version);

            return Ok(result.Results);
        }

        [Authorize(Policy = Permissions.ErrorMessagesArchive)]
        [Route("errors/{messageId:required:minlength(1)}/archive")]
        [HttpPost]
        [HttpPatch]
        public async Task<IActionResult> Archive(string messageId, CancellationToken cancellationToken = default)
        {
            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Archive, Permissions.ErrorMessagesArchive, MessageActionScope.Single,
                resource: messageId, count: 1, operationId: operationId,
                ct => messageSession.Send(new ArchiveMessage { FailedMessageId = messageId, Scope = MessageActionScope.Single }, AuditHeaders.LocalSendOptions(user, operationId), ct), cancellationToken);

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorMessagesView)]
        [Route("archive/groups/id/{groupId:required:minlength(1)}")]
        [HttpGet]
        public async Task<ActionResult<FailureGroupView>> GetGroup(string groupId, string status = default, string modified = default, CancellationToken cancellationToken = default)
        {
            var result = await dataStore.GetArchivedGroup(groupId, status, modified, cancellationToken);

            Response.WithEtag(result.QueryStats.Version);

            return result.Results == null ? NotFound() : result.Results;
        }
    }
}