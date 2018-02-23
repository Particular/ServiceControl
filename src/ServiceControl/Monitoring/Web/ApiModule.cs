namespace ServiceControl.Monitoring
{
    using System;
    using Nancy;
    using Nancy.ModelBinding;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class EndpointUpdateModel
    {
        public bool MonitorHeartbeat { get; set; }
    }

    public class ApiModule : BaseModule
    {
        public EndpointInstanceMonitoring Monitoring { get; set; }

        public ApiModule()
        {
            Get["/heartbeats/stats"] = _ => Negotiate.WithModel(Monitoring.GetStats());

            Get["/endpoints"] = _ => Negotiate.WithModel(Monitoring.GetEndpoints());

            Patch["/endpoints/{id}"] = parameters =>
            {
                var data = this.Bind<EndpointUpdateModel>();
                var endpointId = (Guid) parameters.id;

                if (data.MonitorHeartbeat)
                {
                    Monitoring.EnableMonitoring(endpointId);
                }
                else
                {
                    Monitoring.DisableMonitoring(endpointId);
                }

                return HttpStatusCode.Accepted;
            };
        }
    }
}