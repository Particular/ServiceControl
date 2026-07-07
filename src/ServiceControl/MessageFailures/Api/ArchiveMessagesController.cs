namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;
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
    public class ArchiveMessagesController(IMessageSession messageSession, IErrorMessageDataStore dataStore, ICurrentUserAccessor userAccessor, IMessageActionAuditLog auditLog) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorMessagesArchive)]
        [Route("errors/archive")]
        [HttpPost]
        [HttpPatch]
        public async Task<IActionResult> ArchiveBatch(string[] messageIds)
        {
            if (messageIds.Any(string.IsNullOrEmpty))
            {
                ModelState.AddModelError(nameof(messageIds), "Cannot contain null or empty message IDs.");
                return UnprocessableEntity(ModelState);
            }

            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Archive, Permissions.ErrorMessagesArchive, MessageActionScope.Batch,
                resource: null, count: messageIds.Length, operationId: operationId, async () =>
                {
                    foreach (var id in messageIds)
                    {
                        await messageSession.Send(new ArchiveMessage { FailedMessageId = id, Scope = MessageActionScope.Batch }, AuditHeaders.LocalSendOptions(user, operationId));
                    }
                });

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorMessagesView)]
        [Route("errors/groups/{classifier?}")]
        [HttpGet]
        public async Task<IActionResult> GetArchiveMessageGroups(string classifier = "Exception Type and Stack Trace")
        {
            var results = await dataStore.GetFailureGroupsByClassifier(classifier);

            Response.WithDeterministicEtag(EtagHelper.CalculateEtag(results));

            return Ok(results);
        }

        [Authorize(Policy = Permissions.ErrorMessagesArchive)]
        [Route("errors/{messageId:required:minlength(1)}/archive")]
        [HttpPost]
        [HttpPatch]
        public async Task<IActionResult> Archive(string messageId)
        {
            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Archive, Permissions.ErrorMessagesArchive, MessageActionScope.Single,
                resource: messageId, count: 1, operationId: operationId,
                () => messageSession.Send(new ArchiveMessage { FailedMessageId = messageId, Scope = MessageActionScope.Single }, AuditHeaders.LocalSendOptions(user, operationId)));

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorMessagesView)]
        [Route("archive/groups/id/{groupId:required:minlength(1)}")]
        [HttpGet]
        public async Task<ActionResult<FailureGroupView>> GetGroup(string groupId, string status = default, string modified = default)
        {
            var result = await dataStore.GetFailureGroupView(groupId, status, modified);

            Response.WithEtag(result.QueryStats.ETag);

            return result.Results;
        }
    }
}