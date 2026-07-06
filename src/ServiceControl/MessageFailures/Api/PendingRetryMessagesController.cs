namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using InternalMessages;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;

    [ApiController]
    [Route("api")]
    public class PendingRetryMessagesController(IMessageSession session, ICurrentUserAccessor userAccessor, IMessageActionAuditLog auditLog) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorMessagesRetry)]
        [Route("pendingretries/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryBy(string[] ids)
        {
            if (ids.Any(string.IsNullOrEmpty))
            {
                ModelState.AddModelError(nameof(ids), "Cannot contain null or empty message IDs.");
                return UnprocessableEntity(ModelState);
            }

            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.Batch,
                resource: null, count: ids.Length, operationId: operationId,
                () => session.Send<RetryPendingMessagesById>(m => m.MessageUniqueIds = ids, AuditHeaders.LocalSendOptions(user, operationId)));

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorMessagesRetry)]
        [Route("pendingretries/queues/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryBy(PendingRetryRequest request)
        {
            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.Queue,
                resource: request.QueueAddress, count: null, operationId: operationId,
                () => session.Send<RetryPendingMessages>(m =>
                {
                    m.QueueAddress = request.QueueAddress;
                    m.PeriodFrom = request.From;
                    m.PeriodTo = request.To;
                }, AuditHeaders.LocalSendOptions(user, operationId)));

            return Accepted();
        }

        public class PendingRetryRequest
        {
            [JsonPropertyName("queueaddress")]
            [MinLength(1)]
            public required string QueueAddress { get; set; }

            [JsonPropertyName("from")]
            public DateTime From { get; set; }

            [JsonPropertyName("to")]
            public DateTime To { get; set; }
        }
    }
}