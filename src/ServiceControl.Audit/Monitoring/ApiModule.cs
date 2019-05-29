namespace ServiceControl.Audit.Monitoring
{
    using Auditing.MessagesView;
    using Infrastructure.Nancy.Modules;

    class ApiModule : BaseModule
    {
        public ApiModule()
        {
            Get["/endpoints/known", true] = (_, token) => KnownEndpointsApi.Execute(this);
        }

        public GetKnownEndpointsApi KnownEndpointsApi { get; set; }
    }
}