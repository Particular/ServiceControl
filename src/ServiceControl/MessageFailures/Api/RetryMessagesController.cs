namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using InternalMessages;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.Auth.Rbac;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Infrastructure.WebApi.Auth;
    using ServiceControl.Persistence;
    using Yarp.ReverseProxy.Forwarder;

    [ApiController]
    [Route("api")]
    public class RetryMessagesController(
        Settings settings,
        HttpMessageInvoker httpMessageInvoker,
        IHttpForwarder forwarder,
        IMessageSession messageSession,
        IErrorMessageDataStore errorMessageDataStore,
        IResourceScopeChecker resourceScopeChecker,
        ILogger<RetryMessagesController> logger) : ControllerBase
    {
        /// <summary>
        /// Retries a single failed message by its ID.
        /// Requires <c>messages:retry</c> permission (verb gate via policy),
        /// plus an inline resource-scope check against the message's queue address.
        /// </summary>
        [RequirePermission(Permissions.MessagesRetry)]
        [Authorize(Policy = Permissions.MessagesRetry)]
        [Route("errors/{failedMessageId:required:minlength(1)}/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryMessageBy([FromQuery(Name = "instance_id")] string instanceId, string failedMessageId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || instanceId == settings.InstanceId)
            {
                // Local retry: load the message to perform the resource-scope check.
                var message = await errorMessageDataStore.ErrorBy(failedMessageId);
                if (message == null)
                {
                    return NotFound();
                }

                // Resource-scope check: is this message's queue address in scope for this user?
                // IResourceScopeChecker writes the structured 403 body on deny and returns non-null.
                var queueAddress = message.ProcessingAttempts
                    .LastOrDefault()
                    ?.FailureDetails
                    ?.AddressOfFailingEndpoint;

                var scopeDeny = await resourceScopeChecker.EnforceAsync(
                    User, Permissions.MessagesRetry, queueAddress, HttpContext);

                if (scopeDeny != null)
                {
                    // Response body already written by the checker; return Empty to suppress MVC output.
                    return Empty;
                }

                await messageSession.SendLocal<RetryMessage>(m => m.FailedMessageId = failedMessageId);
                return Accepted();
            }

            var remote = settings.RemoteInstances.SingleOrDefault(r => r.InstanceId == instanceId);

            if (remote == null)
            {
                return BadRequest();
            }

            // Remote instance: forward the request — the remote instance enforces its own authorization.
            var forwarderError = await forwarder.SendAsync(HttpContext, remote.BaseAddress, httpMessageInvoker);
            if (forwarderError != ForwarderError.None && HttpContext.GetForwarderErrorFeature()?.Exception is { } exception)
            {
                logger.LogWarning(exception, "Failed to forward the request to remote instance at {RemoteInstanceUrl}", remote.BaseAddress + HttpContext.Request.GetEncodedPathAndQuery());
            }

            return Empty;
        }

        [RequirePermission(Permissions.MessagesRetry)]
        [Authorize(Policy = Permissions.MessagesRetry)]
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

        [RequirePermission(Permissions.MessagesRetry)]
        [Authorize(Policy = Permissions.MessagesRetry)]
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

        [RequirePermission(Permissions.MessagesRetry)]
        [Authorize(Policy = Permissions.MessagesRetry)]
        [Route("errors/retry/all")]
        [HttpPost]
        public async Task<IActionResult> RetryAll()
        {
            await messageSession.SendLocal(new RequestRetryAll());

            return Accepted();
        }

        [RequirePermission(Permissions.MessagesRetry)]
        [Authorize(Policy = Permissions.MessagesRetry)]
        [Route("errors/{endpointName:required:minlength(1)}/retry/all")]
        [HttpPost]
        public async Task<IActionResult> RetryAllByEndpoint(string endpointName)
        {
            await messageSession.SendLocal(new RequestRetryAll { Endpoint = endpointName });

            return Accepted();
        }
    }
}
