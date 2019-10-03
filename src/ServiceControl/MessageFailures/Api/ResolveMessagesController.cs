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
    using InternalMessages;
    using NServiceBus;

    public class ResolveMessagesController : ApiController
    {
        internal ResolveMessagesController(IMessageSession messageSession)
        {
            this.messageSession = messageSession;
        }

        [Route("pendingretries/resolve")]
        [HttpPatch]
        public async Task<HttpResponseMessage> ResolveBy(ResolveRequest request)
        {
            if (request.uniquemessageids != null)
            {
                if (request.uniquemessageids.Any(string.IsNullOrEmpty))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest);
                }

                foreach (var id in request.uniquemessageids)
                {
                    await messageSession.SendLocal(new MarkPendingRetryAsResolved {FailedMessageId = id})
                        .ConfigureAwait(false);
                }

                return Request.CreateResponse(HttpStatusCode.Accepted);
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

            await messageSession.SendLocal<MarkPendingRetriesAsResolved>(m =>
            {
                m.PeriodFrom = from;
                m.PeriodTo = to;
            }).ConfigureAwait(false);

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        [Route("pendingretries/queues/resolve")]
        [HttpPatch]
        public async Task<HttpResponseMessage> ResolveByQueue(ResolveRequest request)
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

            await messageSession.SendLocal<MarkPendingRetriesAsResolved>(m =>
            {
                m.QueueAddress = request.queueaddress;
                m.PeriodFrom = from;
                m.PeriodTo = to;
            }).ConfigureAwait(false);

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        readonly IMessageSession messageSession;

        public class ResolveRequest
        {
            public string queueaddress { get; set; }
            public List<string> uniquemessageids { get; set; }
            public string from { get; set; }
            public string to { get; set; }
        }
    }
}