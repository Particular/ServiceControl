namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;
    using InternalMessages;
    using NServiceBus;

    public class UnArchiveMessagesController : ApiController
    {
        internal UnArchiveMessagesController(IMessageSession messageSession)
        {
            this.messageSession = messageSession;
        }

        [Route("errors/unarchive")]
        [HttpPatch]
        public async Task<StatusCodeResult> Unarchive(List<string> ids)
        {
            if (ids.Any(string.IsNullOrEmpty))
            {
                return StatusCode(HttpStatusCode.BadRequest);
            }

            var request = new UnArchiveMessages { FailedMessageIds = ids };

            await messageSession.SendLocal(request).ConfigureAwait(false);

            return StatusCode(HttpStatusCode.Accepted);
        }

        [Route("errors/{from}...{to}/unarchive")]
        [HttpPatch]
        public async Task<StatusCodeResult> Unarchive(string from, string to)
        {
            DateTime fromDateTime, toDateTime;

            try
            {
                fromDateTime = DateTime.Parse(from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                toDateTime = DateTime.Parse(to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            catch (Exception)
            {
                return StatusCode(HttpStatusCode.BadRequest);
            }

            await messageSession.SendLocal(new UnArchiveMessagesByRange
            {
                From = fromDateTime,
                To = toDateTime,
                CutOff = DateTime.UtcNow
            }).ConfigureAwait(false);

            return StatusCode(HttpStatusCode.Accepted);
        }

        readonly IMessageSession messageSession;
    }
}