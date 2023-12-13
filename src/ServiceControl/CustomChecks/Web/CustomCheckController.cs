namespace ServiceControl.CustomChecks
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    [ApiController]
    public class CustomCheckController(ICustomChecksDataStore checksDataStore, IMessageSession session)
        : ControllerBase
    {
        [Route("customchecks")]
        [HttpGet]
        public async Task<IList<CustomCheck>> CustomChecks([FromQuery] PagingInfo pagingInfo, string status = null)
        {
            var stats = await checksDataStore.GetStats(pagingInfo, status);

            Response.WithPagingLinksAndTotalCount(pagingInfo, stats.QueryStats.TotalCount);
            Response.WithEtag(stats.QueryStats.ETag);

            return stats.Results;
        }

        [Route("customchecks/{id}")]
        [HttpDelete]
        public async Task<IActionResult> Delete(Guid id)
        {
            await session.SendLocal(new DeleteCustomCheck { Id = id });

            return Accepted();
        }
    }
}