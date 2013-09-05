namespace ServiceControl.BusinessMonitoring
{
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using Nancy;

    public class ApiModule : BaseModule
    {
        public ApiModule()
        {
            Get["/endpoints/{name}/sla"] = parameters =>
            {
                string endpoint = parameters.name;

                return Negotiate.WithModel(new
                {
                    Current = EndpointSLAMonitoring.GetSLAFor(endpoint)
                });
            };
        }

        public EndpointSLAMonitoring EndpointSLAMonitoring { get; set; }
    }
}