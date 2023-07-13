namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.WebApi;
    using Persistence;

    class GetErrorByIdController : ApiController
    {
        public GetErrorByIdController(IErrorMessageDataStore dataStore)
        {
            this.dataStore = dataStore;
        }

        [Route("errors/{failedmessageid:guid}")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorBy(Guid failedMessageId)
        {
            var result = await dataStore.ErrorBy(failedMessageId)
                .ConfigureAwait(false);

            if (result == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }

            return Negotiator.FromModel(Request, result);
        }

        [Route("errors/last/{failedmessageid:guid}")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorLastBy(Guid failedMessageId)
        {
            var result = await dataStore.ErrorLastBy(failedMessageId)
                .ConfigureAwait(false);

            if (result == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }


            return Negotiator.FromModel(Request, result);
        }

        readonly IErrorMessageDataStore dataStore;
    }
}