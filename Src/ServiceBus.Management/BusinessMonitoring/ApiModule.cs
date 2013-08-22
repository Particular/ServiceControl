namespace ServiceBus.Management.BusinessMonitoring
{
    using Modules;
    using global::Nancy;

    public class ApiModule : BaseModule
    {
        public EndpointSLAMonitoring EndpointSLAMonitoring { get; set; }
        
        
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
    }
}