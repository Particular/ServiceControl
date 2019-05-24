namespace ServiceControl.Audit.Monitoring
{
    using CompositeViews.Endpoints;
    using CompositeViews.Messages;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    class ApiModule : BaseModule
    {
        public ApiModule()
        {
            Get["/endpoints/known", true] = (_, token) => KnownEndpointsApi.Execute(this);
        }

        public GetKnownEndpointsApi KnownEndpointsApi { get; set; }
    }
}