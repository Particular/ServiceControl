namespace ServiceControl.Audit.SagaAudit
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    class SagasController : ApiController
    {
        public SagasController(GetSagaByIdApi getSagaByIdApi)
        {
            this.getSagaByIdApi = getSagaByIdApi;
        }

        [Route("sagas/{id}")]
        [HttpGet]
        public Task<HttpResponseMessage> Sagas(Guid id) => getSagaByIdApi.Execute(this, id);

        readonly GetSagaByIdApi getSagaByIdApi;
    }
}