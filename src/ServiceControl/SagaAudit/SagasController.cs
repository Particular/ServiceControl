namespace ServiceControl.SagaAudit
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;

    [ApiController]
    [Route("api")]
    public class SagasController(GetSagaByIdApi getSagaByIdApi) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorSagasView)]
        [Route("sagas/{id}")]
        [HttpGet]
        public async Task<SagaHistory> Sagas([FromQuery] PagingInfo pagingInfo, Guid id)
        {
            QueryResult<SagaHistory> result =
                await getSagaByIdApi.Execute(new SagaByIdContext(pagingInfo, id), Request.GetEncodedPathAndQuery());

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }
    }
}