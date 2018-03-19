namespace ServiceControl.Monitoring
{
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.CompositeViews.Messages;

    public class ApiModule : BaseModule
    {
        public GetKnownEndpointsApi KnownEndpointsApi { get; set; }

        public ApiModule()
        {
            Get["/endpoints/known", true] = (_, token) => KnownEndpointsApi.Execute(this);
        }
    }
}