namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    class GetAllMessagesApi : ScatterGatherApiMessageView<NoInput>
    {
        public GetAllMessagesApi(IErrorMessageDataStore dataStore, Settings settings, Func<HttpClient> httpClientFactory) : base(dataStore, settings, httpClientFactory)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> LocalQuery(HttpRequestMessage request, NoInput input)
        {
            return await DataStore.GetAllMessages(request).ConfigureAwait(false);
        }
    }
}