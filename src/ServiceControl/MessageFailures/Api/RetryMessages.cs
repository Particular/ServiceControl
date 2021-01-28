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
    using Recoverability;

    public class RetryMessagesController : ApiController
    {
        internal RetryMessagesController(RetryMessagesApi retryMessagesApi, IMessageSession messageSession)
        {
            this.messageSession = messageSession;
            this.retryMessagesApi = retryMessagesApi;
        }

        [Route("errors/{failedmessageid}/retry")]
        [HttpPost]
        public async Task<HttpResponseMessage> RetryMessageBy(string failedMessageId)
        {
            if (string.IsNullOrEmpty(failedMessageId))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            return await retryMessagesApi.Execute(this, failedMessageId).ConfigureAwait(false);
        }

        [Route("errors/retry")]
        [HttpPost]
        public async Task<HttpResponseMessage> RetryAllBy(List<string> messageIds)
        {
            if (messageIds.Any(string.IsNullOrEmpty))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            await messageSession.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = messageIds.ToArray())
                .ConfigureAwait(false);

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        [Route("errors/queues/{queueaddress}/retry")]
        [HttpPost]
        public async Task<HttpResponseMessage> RetryAllBy(string queueAddress)
        {
            if (string.IsNullOrWhiteSpace(queueAddress))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "queueaddress URL parameter must be provided");
            }

            await messageSession.SendLocal<RetryMessagesByQueueAddress>(m =>
            {
                m.QueueAddress = queueAddress;
                m.Status = FailedMessageStatus.Unresolved;
            }).ConfigureAwait(false);

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        [Route("errors/retry/all")]
        [HttpPost]
        public async Task<HttpResponseMessage> RetryAll()
        {
            await messageSession.SendLocal(new RequestRetryAll())
                .ConfigureAwait(false);

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        [Route("errors/{endpointname}/retry/all")]
        [HttpPost]
        public async Task<HttpResponseMessage> RetryAllByEndpoint(string endpointName)
        {
            await messageSession.SendLocal(new RequestRetryAll { Endpoint = endpointName })
                .ConfigureAwait(false);

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        readonly RetryMessagesApi retryMessagesApi;
        readonly IMessageSession messageSession;
    }
}