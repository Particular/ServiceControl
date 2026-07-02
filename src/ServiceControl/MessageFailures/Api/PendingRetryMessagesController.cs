namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
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
            var operationId = Guid.NewGuid().ToString("N");
            auditLog.Operation(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.Batch,
                resource: null, count: ids.Length, operationId: operationId);
            foreach (var id in ids)
            {
                auditLog.MessageAction(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry,
                    MessageActionScope.Batch, messageId: id, operationId: operationId);
            }

            await session.SendLocal<RetryPendingMessagesById>(m => m.MessageUniqueIds = ids);

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorMessagesRetry)]
        [Route("pendingretries/queues/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryBy(PendingRetryRequest request)
        {
            auditLog.Operation(userAccessor.Resolve(User), MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.Queue,
                resource: request.QueueAddress, count: null, operationId: Guid.NewGuid().ToString("N"));

            await session.SendLocal<RetryPendingMessages>(m =>
            {
                m.QueueAddress = request.QueueAddress;
                m.PeriodFrom = request.From;
                m.PeriodTo = request.To;
            });

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