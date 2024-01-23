namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using InternalMessages;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;
    using Yarp.ReverseProxy.Forwarder;

    [ApiController]
    [Route("api")]
    public class RetryMessagesController(Settings settings, HttpMessageInvoker httpMessageInvoker, IHttpForwarder forwarder, IMessageSession messageSession) : ControllerBase
    {
        [Route("errors/{failedmessageid}/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryMessageBy([FromQuery(Name = "instance_id")] string instanceId, string failedMessageId)
        {
            if (string.IsNullOrEmpty(failedMessageId))
            {
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(instanceId) || instanceId == settings.InstanceId)
            {
                await messageSession.SendLocal<RetryMessage>(m => m.FailedMessageId = failedMessageId);
                return Accepted();
            }

            var remote = settings.RemoteInstances.SingleOrDefault(r => r.InstanceId == instanceId);

            if (remote == null)
            {
                return BadRequest();
            }

            await forwarder.SendAsync(HttpContext, remote.ApiUri, httpMessageInvoker);

            return StatusCode(Response.StatusCode);
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
    }
}