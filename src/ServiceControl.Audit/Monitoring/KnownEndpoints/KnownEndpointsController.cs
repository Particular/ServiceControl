namespace ServiceControl.Audit.Monitoring
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;

    [ApiController]
    [Route("api")]
    public class KnownEndpointsController(IAuditDataStore dataStore) : ControllerBase
    {
        [Route("endpoints/known")]
        [HttpGet]
        public async Task<IList<KnownEndpointsView>> GetAll([FromQuery] PagingInfo pagingInfo)
        {
            var result = await dataStore.QueryKnownEndpoints();
            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }
    }
}