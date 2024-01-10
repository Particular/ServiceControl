namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using InternalMessages;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;

    [ApiController]
    [Route("api")]
    public class ArchiveMessagesController(IMessageSession messageSession, IErrorMessageDataStore dataStore) : ControllerBase
    {
        [Route("errors/archive")]
        [HttpPost]
        [HttpPatch]
        public async Task<IActionResult> ArchiveBatch(List<string> messageIds)
        {
            if (messageIds.Any(string.IsNullOrEmpty))
            {
                return BadRequest();
            }

            foreach (var id in messageIds)
            {
                var request = new ArchiveMessage { FailedMessageId = id };

                await messageSession.SendLocal(request);
            }

            return Accepted();
        }

        [Route("errors/groups/{classifier?}")]
        [HttpGet]
        public async Task<IActionResult> GetArchiveMessageGroups(string classifier = "Exception Type and Stack Trace")
        {
            var results = await dataStore.GetFailureGroupsByClassifier(classifier);

            Response.WithDeterministicEtag(EtagHelper.CalculateEtag(results));

            return Ok(results);
        }

        [Route("errors/{messageId}/archive")]
        [HttpPost]
        [HttpPatch]
        public async Task<IActionResult> Archive(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                return BadRequest();
            }

            await messageSession.SendLocal<ArchiveMessage>(m => m.FailedMessageId = messageId);

            return Accepted();
        }

        [Route("archive/groups/id/{groupId}")]
        [HttpGet]
        public async Task<ActionResult<FailureGroupView>> GetGroup(string groupId, string status = default, string modified = default)
        {
            var result = await dataStore.GetFailureGroupView(groupId, status, modified);

            Response.WithEtag(result.QueryStats.ETag);

            return result.Results;
        }
    }
}