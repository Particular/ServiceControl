namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using InternalMessages;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Infrastructure.Auth.Rbac;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Infrastructure.WebApi.Auth;

    [ApiController]
    [Route("api")]
    public class PendingRetryMessagesController(IMessageSession session) : ControllerBase
    {
        [RequirePermission(Permissions.MessagesRetry)]
        [Authorize(Policy = Permissions.MessagesRetry)]
        [Route("pendingretries/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryBy(string[] ids)
        {
            if (ids.Any(string.IsNullOrEmpty))
            {
                ModelState.AddModelError(nameof(ids), "Cannot contain null or empty message IDs.");
                return UnprocessableEntity(ModelState);
            }

            await session.SendLocal<RetryPendingMessagesById>(m => m.MessageUniqueIds = ids);

            return Accepted();
        }

        [RequirePermission(Permissions.MessagesRetry)]
        [Authorize(Policy = Permissions.MessagesRetry)]
        [Route("pendingretries/queues/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryBy(PendingRetryRequest request)
        {
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
