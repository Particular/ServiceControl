namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Autofac;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceBus.Management.Infrastructure.Settings;

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
        public IDocumentStore Store { get; set; }
        public Settings Settings { get; set; }
        public Func<HttpClient> HttpClientFactory { get; set; }

        public async Task<dynamic> Execute(BaseModule module, TIn input)
        {
            var currentRequest = module.Request;

            var response = await Query(currentRequest, input).ConfigureAwait(false);
            var negotiate = module.Negotiate;
            return negotiate.WithQueryResult(response, currentRequest);
        }

        public abstract Task<QueryResult<TOut>> Query(Request request, TIn input);
    }
}