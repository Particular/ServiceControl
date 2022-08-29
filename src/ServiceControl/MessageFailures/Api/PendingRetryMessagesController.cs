namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;
    using InternalMessages;
    using NServiceBus;

    class PendingRetryMessagesController : ApiController
    {
        public PendingRetryMessagesController(IMessageSession messageSession)
        {
            this.messageSession = messageSession;
        }

        [Route("pendingretries/retry")]
        [HttpPost]
        public async Task<StatusCodeResult> RetryBy(List<string> ids)
        {
            if (ids.Any(string.IsNullOrEmpty))
            {
                return StatusCode(HttpStatusCode.BadRequest);
            }

            await messageSession.SendLocal<RetryPendingMessagesById>(m => m.MessageUniqueIds = ids.ToArray())
                .ConfigureAwait(false);

            return StatusCode(HttpStatusCode.Accepted);
        }

        [Route("pendingretries/queues/retry")]
        [HttpPost]
        public async Task<HttpResponseMessage> RetryBy(PendingRetryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.queueaddress))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "QueueAddress");
            }

            DateTime from, to;

            try
            {
                from = DateTime.Parse(request.from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                to = DateTime.Parse(request.to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            catch (Exception)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "From/To");
            }

            await messageSession.SendLocal<RetryPendingMessages>(m =>
            {
                m.QueueAddress = request.queueaddress;
                m.PeriodFrom = from;
                m.PeriodTo = to;
            }).ConfigureAwait(false);

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        readonly IMessageSession messageSession;

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