namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using InternalMessages;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;

    [ApiController]
    [Route("api")]
    public class UnArchiveMessagesController(IMessageSession session) : ControllerBase
    {
        [Route("errors/unarchive")]
        [HttpPatch]
        public async Task<IActionResult> Unarchive(string[] ids)
        {
            if (ids.Any(string.IsNullOrEmpty))
            {
                return BadRequest();
            }

            var request = new UnArchiveMessages { FailedMessageIds = ids };

            await session.SendLocal(request);

            return Accepted();
        }

        [Route("errors/{from}...{to}/unarchive")]
        [HttpPatch]
        public async Task<IActionResult> Unarchive(string from, string to)
        {
            DateTime fromDateTime, toDateTime;

            try
            {
                fromDateTime = DateTime.Parse(from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                toDateTime = DateTime.Parse(to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            catch (Exception)
            {
                return BadRequest();
            }

            await session.SendLocal(new UnArchiveMessagesByRange { From = fromDateTime, To = toDateTime });

            return Accepted();
        }
    }
}