namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;
    using Persistence.Infrastructure;

    [ApiController]
    [Route("api")]
    public class GetErrorByIdController(IErrorMessageDataStore store) : ControllerBase
    {
        [Route("errors/{failedMessageId:required:minlength(1)}")]
        [HttpGet]
        public async Task<ActionResult<FailedMessage>> ErrorBy(string failedMessageId)
        {
            var result = await store.ErrorBy(failedMessageId);
            if (result == null)
            {
                return NotFound();
            }

            var authInfo = AuthorizationInfo.FromClaims(HttpContext.User);
            if (!authInfo.IsQueueReadable(result.ProcessingAttempts.Last().FailureDetails.AddressOfFailingEndpoint))
            {
                return NotFound();
            }

            return result;
        }

        [Route("errors/last/{failedMessageId:required:minlength(1)}")]
        [HttpGet]
        public async Task<ActionResult<FailedMessageView>> ErrorLastBy(string failedMessageId)
        {
            var result = await store.ErrorLastBy(failedMessageId);
            if (result == null)
            {
                return NotFound();
            }

            var authInfo = AuthorizationInfo.FromClaims(HttpContext.User);
            if (!authInfo.IsQueueReadable(result.QueueAddress))
            {
                return NotFound();
            }

            return result;
        }
    }
}