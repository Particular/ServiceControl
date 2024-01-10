namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using InternalMessages;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using Recoverability;

    [ApiController]
    [Route("api")]
    public class RetryMessagesController : ControllerBase
    {
        public RetryMessagesController(RetryMessagesApi retryMessagesApi, IMessageSession messageSession)
        {
            this.messageSession = messageSession;
            this.retryMessagesApi = retryMessagesApi;
        }

        [Route("errors/{failedmessageid}/retry")]
        [HttpPost]
        public async Task<HttpResponseMessage> RetryMessageBy([FromQuery(Name = "instance_id")] string instanceId, string failedMessageId)
        {
            // TODO we probably can't stream this directly. See https://stackoverflow.com/questions/54136488/correct-way-to-return-httpresponsemessage-as-iactionresult-in-net-core-2-2
            // Revisit once we have things compiling

            if (string.IsNullOrEmpty(failedMessageId))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            return await retryMessagesApi.Execute(new RetryMessagesApiContext(instanceId, failedMessageId));
        }

        [Route("errors/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryAllBy(List<string> messageIds)
        {
            if (messageIds.Any(string.IsNullOrEmpty))
            {
                return BadRequest();
            }

            await messageSession.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = messageIds.ToArray());

            return Accepted();
        }

        [Route("errors/queues/{queueaddress}/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryAllBy(string queueAddress)
        {
            if (string.IsNullOrWhiteSpace(queueAddress))
            {
                // TODO previously it was using Request.CreateErrorResponse(HttpStatusCode.BadRequest, "QueueAddress") which might be returning a complex object
                return BadRequest("queueaddress URL parameter must be provided");
            }

            await messageSession.SendLocal<RetryMessagesByQueueAddress>(m =>
            {
                m.QueueAddress = queueAddress;
                m.Status = FailedMessageStatus.Unresolved;
            });

            return Accepted();
        }

        [Route("errors/retry/all")]
        [HttpPost]
        public async Task<IActionResult> RetryAll()
        {
            await messageSession.SendLocal(new RequestRetryAll());

            return Accepted();
        }

        [Route("errors/{endpointname}/retry/all")]
        [HttpPost]
        public async Task<IActionResult> RetryAllByEndpoint(string endpointName)
        {
            await messageSession.SendLocal(new RequestRetryAll { Endpoint = endpointName });

            return Accepted();
        }

        readonly RetryMessagesApi retryMessagesApi;
        readonly IMessageSession messageSession;
    }
}