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
    [Route("api")]
    public class ResolveMessagesController(IMessageSession session) : ControllerBase
    {
        [Route("pendingretries/resolve")]
        [HttpPatch]
        public async Task<IActionResult> ResolveBy(ResolveRequest request)
        {
            if (request.uniquemessageids != null)
            {
                if (request.uniquemessageids.Any(string.IsNullOrEmpty))
                {
                    return BadRequest();
                }

                foreach (var id in request.uniquemessageids)
                {
                    await session.SendLocal(new MarkPendingRetryAsResolved { FailedMessageId = id });
                }

                return Accepted();
            }

            DateTime from, to;

            try
            {
                from = DateTime.Parse(request.from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                to = DateTime.Parse(request.to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            catch (Exception)
            {
                // TODO previously it was using Request.CreateErrorResponse(HttpStatusCode.BadRequest, "From/To") which might be returning a complex object
                // Let's verify
                return BadRequest("From/To");
            }

            await session.SendLocal<MarkPendingRetriesAsResolved>(m =>
            {
                m.PeriodFrom = from;
                m.PeriodTo = to;
            });

            return Accepted();
        }

        [Route("pendingretries/queues/resolve")]
        [HttpPatch]
        public async Task<IActionResult> ResolveByQueue(ResolveRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.queueaddress))
            {
                // TODO previously it was using Request.CreateErrorResponse(HttpStatusCode.BadRequest, QueueAddress") which might be returning a complex object
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
                // TODO previously it was using Request.CreateErrorResponse(HttpStatusCode.BadRequest, From/To") which might be returning a complex object
                // Let's verify
                return BadRequest("From/To");
            }

            await session.SendLocal<MarkPendingRetriesAsResolved>(m =>
            {
                m.QueueAddress = request.queueaddress;
                m.PeriodFrom = from;
                m.PeriodTo = to;
            });

            return Accepted();
        }

        public class ResolveRequest
        {
#pragma warning disable IDE1006 // Naming Styles
            public string queueaddress { get; set; }
            public List<string> uniquemessageids { get; set; }
            public string from { get; set; }
            public string to { get; set; }
#pragma warning restore IDE1006 // Naming Styles
        }
    }
}