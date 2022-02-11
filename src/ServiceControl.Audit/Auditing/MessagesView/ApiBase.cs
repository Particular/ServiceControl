namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Autofac;
    using Infrastructure.SQL;
    using Infrastructure.WebApi;

    interface IApi
    {
    }

    class ApisModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(ThisAssembly)
                .AssignableTo<IApi>()
                .AsSelf()
                .AsImplementedInterfaces()
                .PropertiesAutowired();
        }
    }

    abstract class ApiBase<TIn, TOut> : IApi
        where TOut : class
    {
        protected ApiBase(SqlQueryStore queryStore)
        {
            Store = queryStore;
        }

        protected SqlQueryStore Store { get; }

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
        protected ApiBaseNoInput(SqlQueryStore queryStore) : base(queryStore)
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