namespace ServiceControl.Audit.SagaAudit
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api")]
    public class SagasController : ControllerBase
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