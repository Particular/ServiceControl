namespace ServiceControl.SagaAudit
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using ServiceControl.Persistence.Infrastructure;

    [ApiController]
    [Route("api")]
    class SagasController(GetSagaByIdApi getSagaByIdApi) : ControllerBase
    {
        [Route("sagas/{id}")]
        [HttpGet]
        public Task<SagaHistory> Sagas([FromQuery] PagingInfo pagingInfo, Guid id) => getSagaByIdApi.Execute(new SagaByIdContext(pagingInfo, id));
    }
}