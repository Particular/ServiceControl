namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using InternalMessages;
    using NServiceBus;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Net.Http.Headers;
    using ServiceControl.Persistence;

    class ArchiveMessagesController : Controller
    {
        public ArchiveMessagesController(IMessageSession session, IErrorMessageDataStore dataStore)
        {
            messageSession = session;
            this.dataStore = dataStore;
        }

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

            if (results is { Count: > 0 })
            {
                HttpContext.Response.Headers[HeaderNames.ETag] = EtagHelper.CalculateEtag(results);
            }
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

            await messageSession.SendLocal<ArchiveMessage>(m => { m.FailedMessageId = messageId; });

            return Accepted();
        }

        [Route("archive/groups/id/{groupId}")]
        [HttpGet]
        public async Task<IActionResult> GetGroup(string groupId, [FromQuery(Name = "status")] string status = default, [FromQuery(Name = "modified")] string modified = default)
        {
            var result = await dataStore.GetFailureGroupView(groupId, status, modified);

            HttpContext.Response.Headers[HeaderNames.ETag] = result.QueryStats.ETag;
            return Ok(result.Results);
        }

        readonly IErrorMessageDataStore dataStore;
        readonly IMessageSession messageSession;
    }
}