﻿namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using InternalMessages;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using NServiceBus.Logging;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;
    using Yarp.ReverseProxy.Forwarder;

    [ApiController]
    [Route("api")]
    public class RetryMessagesController(Settings settings, HttpMessageInvoker httpMessageInvoker, IHttpForwarder forwarder, IMessageSession messageSession) : ControllerBase
    {
        [Route("errors/{failedMessageId:required:minlength(1)}/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryMessageBy([FromQuery(Name = "instance_id")] string instanceId, string failedMessageId)
        {
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

            var forwarderError = await forwarder.SendAsync(HttpContext, remote.BaseAddress, httpMessageInvoker);
            if (forwarderError != ForwarderError.None && HttpContext.GetForwarderErrorFeature()?.Exception is { } exception)
            {
                logger.Warn($"Failed to forward the request ot remote instance at {remote.BaseAddress + HttpContext.Request.GetEncodedPathAndQuery()}.", exception);
            }

            return Empty;
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

        [Route("errors/queues/{queueAddress:required:minlength(1)}/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryAllBy(string queueAddress)
        {
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

        [Route("errors/{endpointName:required:minlength(1)}/retry/all")]
        [HttpPost]
        public async Task<IActionResult> RetryAllByEndpoint(string endpointName)
        {
            await messageSession.SendLocal(new RequestRetryAll { Endpoint = endpointName });

            return Accepted();
        }

        static ILog logger = LogManager.GetLogger(typeof(RetryMessagesController));
    }
}