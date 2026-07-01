namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using InternalMessages;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;

    [ApiController]
    [Route("api")]
    public class UnArchiveMessagesController(IMessageSession session, ICurrentUserAccessor userAccessor, IMessageActionAuditLog auditLog) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorMessagesUnarchive)]
        [Route("errors/unarchive")]
        [HttpPatch]
        public async Task<IActionResult> Unarchive(string[] ids)
        {
            if (ids.Any(string.IsNullOrEmpty))
            {
                return BadRequest();
            }

            auditLog.Operation(userAccessor.Resolve(User), MessageActionKind.Unarchive, Permissions.ErrorMessagesUnarchive, MessageActionScope.Batch,
                resource: null, count: ids.Length, operationId: Guid.NewGuid().ToString("N"));

            var request = new UnArchiveMessages { FailedMessageIds = ids };

            await session.SendLocal(request);

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorMessagesUnarchive)]
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

            auditLog.Operation(userAccessor.Resolve(User), MessageActionKind.Unarchive, Permissions.ErrorMessagesUnarchive, MessageActionScope.Range,
                resource: $"{from}...{to}", count: null, operationId: Guid.NewGuid().ToString("N"));

            await session.SendLocal(new UnArchiveMessagesByRange { From = fromDateTime, To = toDateTime });

            return Accepted();
        }
    }
}