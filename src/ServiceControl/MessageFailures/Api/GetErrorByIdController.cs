namespace ServiceControl.MessageFailures.Api
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;

    [ApiController]
    [Route("api")]
    public class GetErrorByIdController(IErrorMessageDataStore store) : ControllerBase
    {
        [Route("errors/{failedMessageId:required:minlength(1)}")]
        [HttpGet]
        public async Task<ActionResult<FailedMessage>> ErrorBy(string failedMessageId)
        {
            var result = await store.ErrorBy(failedMessageId);

            return result == null ? NotFound() : result;
        }

        [Route("errors/last/{failedMessageId:required:minlength(1)}")]
        [HttpGet]
        public async Task<ActionResult<FailedMessageView>> ErrorLastBy(string failedMessageId)
        {
            var result = await store.ErrorLastBy(failedMessageId);

            return result == null ? NotFound() : result;
        }
    }
}