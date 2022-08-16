namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.WebApi;
    using ServiceControl.Audit.Persistence;

    interface IApi
    {
    }

    abstract class ApiBase<TIn, TOut> : IApi
        where TOut : class
    {
        protected ApiBase(IAuditDataStore dataStore)
        {
            DataStore = dataStore;
        }

        protected IAuditDataStore DataStore { get; }

        public async Task<HttpResponseMessage> Execute(ApiController controller, TIn input)
        {
            var currentRequest = controller.Request;

            var queryResult = await Query(currentRequest, input).ConfigureAwait(false);
            return Negotiator.FromQueryResult(currentRequest, queryResult);
        }

        protected abstract Task<QueryResult<TOut>> Query(HttpRequestMessage request, TIn input);
    }

    abstract class ApiBaseNoInput<TOut> : ApiBase<NoInput, TOut>
        where TOut : class
    {
        protected ApiBaseNoInput(IAuditDataStore dataStore) : base(dataStore)
        {
        }


        public Task<HttpResponseMessage> Execute(ApiController controller)
        {
            return Execute(controller, NoInput.Instance);
        }

        protected override Task<QueryResult<TOut>> Query(HttpRequestMessage request, NoInput input)
        {
            return Query(request);
        }

        protected abstract Task<QueryResult<TOut>> Query(HttpRequestMessage request);
    }
}