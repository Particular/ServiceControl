namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Operations.BodyStorage.Api;
    using Persistence.Infrastructure;
    using ServiceControl.CompositeViews.MessageCounting;

    // All routes matching `messages/*` must be in this controller as WebAPI cannot figure out the overlapping routes
    // from `messages/{*catchAll}` if they're in separate controllers.
    [ApiController]
    public class GetMessagesController(
        GetAllMessagesApi allMessagesApi,
        GetAllMessagesForEndpointApi allMessagesForEndpointApi,
        GetAuditCountsForEndpointApi auditCountsForEndpointApi,
        GetBodyByIdApi bodyByIdApi,
        SearchApi api,
        SearchEndpointApi endpointApi)
        : ControllerBase
    {
        [Route("messages")]
        [HttpGet]
        public Task<IList<MessagesView>> Messages([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo,
            [FromQuery(Name = "include_system_messages")] bool includeSystemMessages) => allMessagesApi.Execute(
            new(pagingInfo, sortInfo, includeSystemMessages));

        [Route("endpoints/{endpoint}/messages")]
        [HttpGet]
        public Task<IList<MessagesView>> MessagesForEndpoint([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo,
            [FromQuery(Name = "include_system_messages")] bool includeSystemMessages, string endpoint) =>
            allMessagesForEndpointApi.Execute(new(pagingInfo, sortInfo, includeSystemMessages, endpoint));

        // the endpoint name is needed in the route to match the route and forward it as path and query to the remotes
        [Route("endpoints/{endpoint}/audit-count")]
        [HttpGet]
        public Task<IList<AuditCount>> GetEndpointAuditCounts([FromQuery] PagingInfo pagingInfo) =>
            auditCountsForEndpointApi.Execute(new(pagingInfo));

        [Route("messages/{id}/body")]
        [HttpGet]
        public Task<HttpResponseMessage> Get(string id, [FromQuery(Name = "instance_id")] string instanceId)
        {
            // TODO we probably can't stream this directly. See https://stackoverflow.com/questions/54136488/correct-way-to-return-httpresponsemessage-as-iactionresult-in-net-core-2-2
            // Revisit once we have things compiling
            return bodyByIdApi.Execute(new GetByBodyContext(instanceId, id));
        }

        // Possible a message may contain a slash or backslash, either way http.sys will rewrite it to forward slash,
        // and then the "normal" route above will not activate, resulting in 404 if this route is not present.
        [Route("messages/{*catchAll}")]
        [HttpGet]
        public Task<HttpResponseMessage> CatchAll(string catchAll, [FromQuery(Name = "instance_id")] string instanceId)
        {
            if (!string.IsNullOrEmpty(catchAll) && catchAll.EndsWith("/body"))
            {
                var id = catchAll.Substring(0, catchAll.Length - 5);
                return Get(id, instanceId);
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }

        [Route("messages/search")]
        [HttpGet]
        public Task<IList<MessagesView>> Search([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string q) => api.Execute(new(pagingInfo, sortInfo, q));

        [Route("messages/search/{keyword}")]
        [HttpGet]
        public Task<IList<MessagesView>> SearchByKeyWord([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string keyword) =>
            api.Execute(new(pagingInfo, sortInfo, keyword?.Replace("/", @"\")));

        [Route("endpoints/{endpoint}/messages/search")]
        [HttpGet]
        public Task<IList<MessagesView>> Search([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string endpoint, string q) =>
            endpointApi.Execute(new(pagingInfo, sortInfo, endpoint, q));

        [Route("endpoints/{endpoint}/messages/search/{keyword}")]
        [HttpGet]
        public Task<IList<MessagesView>> SearchByKeyword([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string endpoint, string keyword) =>
            endpointApi.Execute(new(pagingInfo, sortInfo, endpoint, keyword));
    }
}