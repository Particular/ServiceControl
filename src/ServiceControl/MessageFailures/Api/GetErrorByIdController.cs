namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;

    [ApiController]
    [Route("api")]
    public class GetErrorByIdController(IErrorMessageDataStore store) : ControllerBase
    {
        [Route("errors/{failedmessageid:guid}")]
        [HttpGet]
        public async Task<ActionResult<FailedMessage>> ErrorBy(Guid failedMessageId)
        {
            var result = await store.ErrorBy(failedMessageId);

            return result == null ? NotFound() : result;
        }

        [Route("errors/last/{failedmessageid:guid}")]
        [HttpGet]
        public async Task<ActionResult<FailedMessageView>> ErrorLastBy(Guid failedMessageId)
        {
            var result = await store.ErrorLastBy(failedMessageId);

            return result == null ? NotFound() : result;
        }
    }
}