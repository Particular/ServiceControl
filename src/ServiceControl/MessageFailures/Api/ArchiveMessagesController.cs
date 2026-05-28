namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using InternalMessages;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Infrastructure.Auth.Rbac;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Infrastructure.WebApi.Auth;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;

    [ApiController]
    [Route("api")]
    public class ArchiveMessagesController(IMessageSession messageSession, IErrorMessageDataStore dataStore) : ControllerBase
    {
        [RequirePermission(Permissions.MessagesArchive)]
        [Authorize(Policy = Permissions.MessagesArchive)]
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

            foreach (var id in messageIds)
            {
                var request = new ArchiveMessage { FailedMessageId = id };

                await messageSession.SendLocal(request);
            }

            return Accepted();
        }

        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("errors/groups/{classifier?}")]
        [HttpGet]
        public async Task<IActionResult> GetArchiveMessageGroups(string classifier = "Exception Type and Stack Trace")
        {
            var results = await dataStore.GetFailureGroupsByClassifier(classifier);

            Response.WithDeterministicEtag(EtagHelper.CalculateEtag(results));

            return Ok(results);
        }

        [RequirePermission(Permissions.MessagesArchive)]
        [Authorize(Policy = Permissions.MessagesArchive)]
        [Route("errors/{messageId:required:minlength(1)}/archive")]
        [HttpPost]
        [HttpPatch]
        public async Task<IActionResult> Archive(string messageId)
        {
            // NOTE: No per-message resource-scope check here. The archive operation is fire-and-forget via
            // SendLocal — the message is enqueued without loading the FailedMessage first, so there is no
            // queue address available to scope-check at this point. The verb gate (messages:archive) is
            // enforced above; resource-scope enforcement for archive would require loading before enqueue,
            // which is a breaking change to the handler chain. Deferred to a future phase.
            await messageSession.SendLocal<ArchiveMessage>(m => m.FailedMessageId = messageId);

            return Accepted();
        }

        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
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
