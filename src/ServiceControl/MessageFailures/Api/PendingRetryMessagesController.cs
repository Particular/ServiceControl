namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using InternalMessages;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;

    [ApiController]
    public class PendingRetryMessagesController(IMessageSession session) : ControllerBase
    {
        [Route("pendingretries/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryBy(List<string> ids)
        {
            if (ids.Any(string.IsNullOrEmpty))
            {
                return BadRequest();
            }

            await session.SendLocal<RetryPendingMessagesById>(m => m.MessageUniqueIds = ids.ToArray());

            return Accepted();
        }

        [Route("pendingretries/queues/retry")]
        [HttpPost]
        public async Task<IActionResult> RetryBy(PendingRetryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.queueaddress))
            {
                // TODO previously it was using Request.CreateErrorResponse(HttpStatusCode.BadRequest, "QueueAddress") which might be returning a complex object
                // Let's verify
                return BadRequest("QueueAddress");
            }

            DateTime from, to;

            try
            {
                from = DateTime.Parse(request.from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                to = DateTime.Parse(request.to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            catch (Exception)
            {
                // TODO previously it was using Request.CreateErrorResponse(HttpStatusCode.BadRequest, "QueueAddress") which might be returning a complex object
                // Let's verify
                return BadRequest("From/To");
            }

            await session.SendLocal<RetryPendingMessages>(m =>
            {
                m.QueueAddress = request.queueaddress;
                m.PeriodFrom = from;
                m.PeriodTo = to;
            });

            return Accepted();
        }

        public class PendingRetryRequest
        {
#pragma warning disable IDE1006 // Naming Styles
            public string queueaddress { get; set; }
            public string from { get; set; }
            public string to { get; set; }
#pragma warning restore IDE1006 // Naming Styles
        }
    }
}