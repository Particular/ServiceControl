namespace ServiceControl.Audit.Monitoring
{
    using Auditing.MessagesView;
    using Infrastructure.Nancy.Modules;

    class WebApi : BaseModule
    {
        public WebApi()
        {
            Get["/endpoints/known", true] = (parameters, token) => GetKnownEndpointsApi.Execute(this, NoInput.Instance);
        }

        public GetKnownEndpointsApi GetKnownEndpointsApi { get; set; }
    }
}