namespace ServiceControl.Audit.Auditing.MessagesView;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using Infrastructure.WebApi;
using Microsoft.AspNetCore.Mvc;
using Persistence;

[ApiController]
[Route("api")]
public class GetMessages2Controller(IAuditDataStore dataStore) : ControllerBase
{
    [Route("messages2")]
    [HttpGet]
    public async Task<IList<MessagesView>> GetAllMessages(
        [FromQuery] SortInfo sortInfo,
        [FromQuery(Name = "page_size")] int pageSize,
        [FromQuery(Name = "endpoint_name")] string endpointName,
        [FromQuery(Name = "from")] string from,
        [FromQuery(Name = "to")] string to,
        string q,
        CancellationToken cancellationToken)
    {
        QueryResult<IList<MessagesView>> result;
        var pagingInfo = new PagingInfo(pageSize: pageSize);
        if (string.IsNullOrWhiteSpace(endpointName))
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                result = await dataStore.GetMessages(false, pagingInfo, sortInfo, new DateTimeRange(from, to), cancellationToken);
            }
            else
            {
                result = await dataStore.QueryMessages(q, pagingInfo, sortInfo, new DateTimeRange(from, to), cancellationToken);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                result = await dataStore.QueryMessagesByReceivingEndpoint(false, endpointName, pagingInfo, sortInfo,
                    new DateTimeRange(from, to), cancellationToken);
            }
            else
            {
                result = await dataStore.QueryMessagesByReceivingEndpointAndKeyword(endpointName, q, pagingInfo,
                    sortInfo, new DateTimeRange(from, to), cancellationToken);
            }
        }

        Response.WithTotalCount(result.QueryStats.TotalCount);

        return result.Results;
    }
}