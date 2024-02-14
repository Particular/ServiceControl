namespace ServiceControl.Audit.SagaAudit
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;
    using ServiceControl.SagaAudit;

    [ApiController]
    [Route("api")]
    public class SagasController(IAuditDataStore dataStore) : ControllerBase
    {
        [Route("sagas/{id}")]
        [HttpGet]
        public async Task<SagaHistory> Sagas([FromQuery] PagingInfo pagingInfo, Guid id)
        {
            var result = await dataStore.QuerySagaHistoryById(id);
            Response.WithQueryResults(result.QueryStats, pagingInfo);
            return result.Results;
        }
    }
}