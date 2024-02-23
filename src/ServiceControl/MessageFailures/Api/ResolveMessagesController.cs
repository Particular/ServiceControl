#nullable enable

namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using InternalMessages;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using NServiceBus;

    [ApiController]
    [Route("api")]
    public class ResolveMessagesController(IMessageSession session) : ControllerBase
    {
        [Route("pendingretries/resolve")]
        [HttpPatch]
        public async Task<IActionResult> ResolveBy(UniqueMessageIdsModel request)
        {
            if (request.UniqueMessageIds != null)
            {
                if (request.UniqueMessageIds.Any(string.IsNullOrEmpty))
                {
                    ModelState.AddModelError<UniqueMessageIdsModel>(model => model.UniqueMessageIds, "Cannot contain null or empty message IDs.");
                    return UnprocessableEntity(ModelState);
                }

                foreach (var id in request.UniqueMessageIds)
                {
                    await session.SendLocal(new MarkPendingRetryAsResolved { FailedMessageId = id });
                }

                return Accepted();
            }

            if (!request.From.HasValue)
            {
                ModelState.AddModelError<UniqueMessageIdsModel>(model => model.From, "Cannot be null when 'UniqueMessageIds' are not provided.");
            }

            if (!request.To.HasValue)
            {
                ModelState.AddModelError<UniqueMessageIdsModel>(model => model.To, "Cannot be null when 'UniqueMessageIds' are not provided.");
            }

            if (!ModelState.IsValid)
            {
                return UnprocessableEntity(ModelState);
            }

            await session.SendLocal<MarkPendingRetriesAsResolved>(m =>
            {
                m.PeriodFrom = request.From!.Value;
                m.PeriodTo = request.To!.Value;
            });

            return Accepted();
        }

        [Route("pendingretries/queues/resolve")]
        [HttpPatch]
        public async Task<IActionResult> ResolveBy(QueueModel queueModel)
        {
            await session.SendLocal<MarkPendingRetriesAsResolved>(m =>
            {
                m.QueueAddress = queueModel.QueueAddress;
                m.PeriodFrom = queueModel.From;
                m.PeriodTo = queueModel.To;
            });

            return Accepted();
        }

        public class UniqueMessageIdsModel
        {
            [JsonPropertyName("uniquemessageids")]
            public List<string>? UniqueMessageIds { get; set; }

            [JsonPropertyName("from")]
            public DateTime? From { get; set; }

            [JsonPropertyName("to")]
            public DateTime? To { get; set; }
        }

        public class QueueModel
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