namespace ServiceControl.CustomChecks
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;

    public class CustomCheckController : ApiController
    {
        internal CustomCheckController(GetCustomChecksApi getApi, DeleteCustomChecksApi deleteApi)
        {
            this.getApi = getApi;
            this.deleteApi = deleteApi;
        }

        [Route("customchecks")]
        [HttpGet]
        public Task<HttpResponseMessage> CustomChecks(string status = null)
        {
            return getApi.Execute(this, status);
        }

        [Route("customchecks/{id}")]
        [HttpDelete]
        public async Task<StatusCodeResult> Delete(Guid id)
        {
            await deleteApi.Execute(this, id).ConfigureAwait(false);

            return StatusCode(HttpStatusCode.Accepted);
        }

        readonly GetCustomChecksApi getApi;
        readonly DeleteCustomChecksApi deleteApi;
    }
}