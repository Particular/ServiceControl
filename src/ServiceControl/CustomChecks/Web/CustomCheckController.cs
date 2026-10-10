namespace ServiceControl.CustomChecks
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    [ApiController]
    [Route("api")]
    public class CustomCheckController(ICustomChecksDataStore checksDataStore, IMessageSession session)
        : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorCustomChecksView)]
        [Route("customchecks")]
        [HttpGet]
        public async Task<IList<CustomCheckView>> CustomChecks([FromQuery] PagingInfo pagingInfo, string status = null, CancellationToken cancellationToken = default)
        {
            var stats = await checksDataStore.GetStats(pagingInfo, status, cancellationToken);

            Response.WithQueryStatsAndPagingInfo(stats.QueryStats, pagingInfo);

            return stats.Results;
        }

        [Authorize(Policy = Permissions.ErrorCustomChecksDelete)]
        [Route("customchecks/{id}")]
        [HttpDelete]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
        {
            await session.SendLocal(new DeleteCustomCheck { Id = id }, cancellationToken);

            return Accepted();
        }
    }
}