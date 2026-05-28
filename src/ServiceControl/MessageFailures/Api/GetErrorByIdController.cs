namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;
    using ServiceControl.Infrastructure.Auth.Rbac;
    using ServiceControl.Infrastructure.WebApi.Auth;

    [ApiController]
    [Route("api")]
    public class GetErrorByIdController(
        IErrorMessageDataStore store,
        IResourceScopeChecker resourceScopeChecker) : ControllerBase
    {
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("errors/{failedMessageId:required:minlength(1)}")]
        [HttpGet]
        public async Task<ActionResult<FailedMessage>> ErrorBy(string failedMessageId)
        {
            var result = await store.ErrorBy(failedMessageId);

            if (result == null)
            {
                return NotFound();
            }

            // Resource-scope check: is this message's queue address in scope for this user?
            // IResourceScopeChecker writes the structured 403 body on deny and returns non-null.
            var queueAddress = result.ProcessingAttempts
                .LastOrDefault()
                ?.FailureDetails
                ?.AddressOfFailingEndpoint;

            var scopeDeny = await resourceScopeChecker.EnforceAsync(
                User, Permissions.MessagesView, queueAddress, HttpContext);

            if (scopeDeny != null)
            {
                return Empty;
            }

            return result;
        }

        [Authorize(Policy = Permissions.MessagesView)]
        [Route("errors/last/{failedMessageId:required:minlength(1)}")]
        [HttpGet]
        public async Task<ActionResult<FailedMessageView>> ErrorLastBy(string failedMessageId)
        {
            var result = await store.ErrorLastBy(failedMessageId);

            if (result == null)
            {
                return NotFound();
            }

            // Resource-scope check: consistent with ErrorBy — a scoped user must not view
            // a message whose queue is outside their scope.
            var scopeDeny = await resourceScopeChecker.EnforceAsync(
                User, Permissions.MessagesView, result.QueueAddress, HttpContext);

            if (scopeDeny != null)
            {
                return Empty;
            }

            return result;
        }
    }
}
