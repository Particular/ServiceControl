namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using InternalMessages;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;

    [ApiController]
    [Route("api")]
    public class PendingRetryMessagesController(IMessageSession session) : ControllerBase
    {
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