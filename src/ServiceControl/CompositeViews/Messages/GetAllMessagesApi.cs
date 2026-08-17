namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Persistence;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public class GetAllMessagesApi : ScatterGatherApiMessageView<IMessagesViewDataStore, ScatterGatherApiMessageViewWithSystemMessagesContext>
    {
        public GetAllMessagesApi(IMessagesViewDataStore dataStore, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, ILogger<GetAllMessagesApi> logger)
            : base(dataStore, settings, httpClientFactory, httpContextAccessor, logger)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(ScatterGatherApiMessageViewWithSystemMessagesContext input, CancellationToken cancellationToken = default)
        {
            return DataStore.GetAllMessages(input.PagingInfo, input.SortInfo, input.IncludeSystemMessages, input.TimeSentRange, cancellationToken);
        }
    }
}