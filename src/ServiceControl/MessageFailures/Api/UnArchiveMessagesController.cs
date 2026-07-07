namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
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

            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Unarchive, Permissions.ErrorMessagesUnarchive, MessageActionScope.Batch,
                resource: null, count: ids.Length, operationId: operationId,
                () => session.Send(new UnArchiveMessages { FailedMessageIds = ids }, AuditHeaders.LocalSendOptions(user, operationId)));

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

            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            await auditLog.AuditedOperation(user, MessageActionKind.Unarchive, Permissions.ErrorMessagesUnarchive, MessageActionScope.Range,
                resource: $"{from}...{to}", count: null, operationId: operationId,
                () => session.Send(new UnArchiveMessagesByRange { From = fromDateTime, To = toDateTime }, AuditHeaders.LocalSendOptions(user, operationId)));

            return Accepted();
        }
    }
}