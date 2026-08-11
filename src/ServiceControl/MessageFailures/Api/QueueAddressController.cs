namespace ServiceControl.MessageFailures.Api;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Auth;
using Infrastructure.WebApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Persistence.Infrastructure;
using ServiceControl.Persistence;

[ApiController]
[Route("api")]
public class QueueAddressController(IQueueAddressStore store) : ControllerBase
{
    [Authorize(Policy = Permissions.ErrorQueuesView)]
    [Route("errors/queues/addresses")]
    [HttpGet]
    public async Task<IList<QueueAddress>> GetAddresses([FromQuery] PagingInfo pagingInfo, CancellationToken cancellationToken = default)
    {
        var result = await store.GetAddresses(pagingInfo, cancellationToken);

        Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);

        return result.Results;
    }
}