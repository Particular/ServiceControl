namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using InternalMessages;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;
    using Yarp.ReverseProxy.Forwarder;

    [ApiController]
    [Route("api")]
    public class RetryMessagesController(
        Settings settings,
        HttpMessageInvoker httpMessageInvoker,
        IHttpForwarder forwarder,
        IMessageSession messageSession,
        ILogger<RetryMessagesController> logger,
        ICurrentUserAccessor userAccessor,
        IMessageActionAuditLog auditLog) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorMessagesRetry)]
        [Route("errors/{failedMessageId:required:minlength(1)}/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryMessageBy([FromQuery(Name = "instance_id")] string instanceId, string failedMessageId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || instanceId == settings.InstanceId)
            {
                var user = userAccessor.Resolve(User);
                var operationId = this.AuditOperationId();
                await auditLog.AuditedOperation(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.Single,
                    resource: failedMessageId, count: 1, operationId: operationId,
                    () => messageSession.Send<RetryMessage>(m => m.FailedMessageId = failedMessageId, AuditHeaders.LocalSendOptions(user, operationId)));
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
                logger.LogWarning(exception, "Failed to forward the request to remote instance at {RemoteInstanceUrl}", remote.BaseAddress + HttpContext.Request.GetEncodedPathAndQuery());
            }

            return Empty;
        }

        [Authorize(Policy = Permissions.ErrorMessagesRetry)]
        [Route("errors/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryAllBy(List<string> messageIds)
        {
            if (messageIds.Any(string.IsNullOrEmpty))
            {
                return BadRequest();
            }

            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.Batch,
                resource: null, count: messageIds.Count, operationId: operationId,
                () => messageSession.Send<RetryMessagesById>(m => m.MessageUniqueIds = messageIds.ToArray(), AuditHeaders.LocalSendOptions(user, operationId)));

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorMessagesRetry)]
        [Route("errors/queues/{queueAddress:required:minlength(1)}/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryAllBy(string queueAddress)
        {
            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.Queue,
                resource: queueAddress, count: null, operationId: operationId,
                () => messageSession.Send<RetryMessagesByQueueAddress>(m =>
                {
                    m.QueueAddress = queueAddress;
                    m.Status = FailedMessageStatus.Unresolved;
                }, AuditHeaders.LocalSendOptions(user, operationId)));

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorMessagesRetry)]
        [Route("errors/retry/all")]
        [HttpPost]
        public async Task<IActionResult> RetryAll()
        {
            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.All,
                resource: null, count: null, operationId: operationId,
                () => messageSession.Send(new RequestRetryAll(), AuditHeaders.LocalSendOptions(user, operationId)));

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorMessagesRetry)]
        [Route("errors/{endpointName:required:minlength(1)}/retry/all")]
        [HttpPost]
        public async Task<IActionResult> RetryAllByEndpoint(string endpointName)
        {
            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.Endpoint,
                resource: endpointName, count: null, operationId: operationId,
                () => messageSession.Send(new RequestRetryAll { Endpoint = endpointName }, AuditHeaders.LocalSendOptions(user, operationId)));

            return Accepted();
        }
    }
}