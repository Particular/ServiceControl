namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using InternalMessages;
    using NServiceBus;

    public class ArchiveMessagesController : ApiController
    {
        public ArchiveMessagesController(IMessageSession session)
        {
            messageSession = session;
        }

        [Route("errors/archive")]
        [HttpPost]
        [HttpPatch]
        public async Task<HttpResponseMessage> ArchiveBatch(List<string> messageIds)
        {
            if (messageIds.Any(string.IsNullOrEmpty))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            foreach (var id in messageIds)
            {
                var request = new ArchiveMessage {FailedMessageId = id};

                await messageSession.SendLocal(request).ConfigureAwait(false);
            }

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        [Route("errors/{messageid}/archive")]
        [HttpPost]
        [HttpPatch]
        public async Task<HttpResponseMessage> Archive(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            await messageSession.SendLocal<ArchiveMessage>(m => { m.FailedMessageId = messageId; }).ConfigureAwait(false);

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        readonly IMessageSession messageSession;
    }
}