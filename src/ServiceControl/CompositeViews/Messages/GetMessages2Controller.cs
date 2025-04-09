namespace ServiceControl.CompositeViews.Messages;

using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.WebApi;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Persistence.Infrastructure;

[ApiController]
[Route("api")]
public class GetMessages2Controller(
    GetAllMessagesApi allMessagesApi,
    GetAllMessagesForEndpointApi allMessagesEndpointApi,
    SearchApi searchApi,
    SearchEndpointApi searchEndpointApi)
    : ControllerBase
{
    [Route("messages2")]
    [HttpGet]
    public async Task<IList<MessagesView>> Messages(
        [FromQuery] SortInfo sortInfo,
        [FromQuery(Name = "page_size")] int pageSize,
        [FromQuery(Name = "endpoint_name")] string endpointName,
        [FromQuery(Name = "from")] string from,
        [FromQuery(Name = "to")] string to,
        string q)
    {
        QueryResult<IList<MessagesView>> result;
        var pagingInfo = new PagingInfo(pageSize: pageSize);
        if (string.IsNullOrWhiteSpace(endpointName))
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                result = await allMessagesApi.Execute(
                    new ScatterGatherApiMessageViewWithSystemMessagesContext(pagingInfo,
                        sortInfo, false, new DateTimeRange(from, to)),
                    Request.GetEncodedPathAndQuery());
            }
            else
            {
                result = await searchApi.Execute(
                    new SearchApiContext(pagingInfo, sortInfo, q, new DateTimeRange(from, to)),
                    Request.GetEncodedPathAndQuery());
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                result = await allMessagesEndpointApi.Execute(
                    new AllMessagesForEndpointContext(pagingInfo, sortInfo, false,
                        endpointName, new DateTimeRange(from, to)),
                    Request.GetEncodedPathAndQuery());
            }
            else
            {
                result = await searchEndpointApi.Execute(new SearchEndpointContext(pagingInfo, sortInfo, q, endpointName, new DateTimeRange(from, to)),
                    Request.GetEncodedPathAndQuery());
            }
        }

        Response.WithTotalCount(result.QueryStats.TotalCount);

        return result.Results;
    }
}