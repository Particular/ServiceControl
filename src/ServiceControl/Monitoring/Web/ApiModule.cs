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

    class ApiModule : BaseModule
    {
        public ApiModule()
        {
            Get["/heartbeats/stats"] = _ => Negotiate.WithModel(Monitoring.GetStats());

            Get["/endpoints"] = _ => Negotiate.WithModel(Monitoring.GetEndpoints());

            Get["/endpoints/known"] = _ => Negotiate.WithModel(Monitoring.GetKnownEndpoints());

            Patch["/endpoints/{id}", true] = async (parameters, token) =>
            {
                var data = this.Bind<EndpointUpdateModel>();
                var endpointId = (Guid)parameters.id;

                if (data.MonitorHeartbeat)
                {
                    await Monitoring.EnableMonitoring(endpointId)
                        .ConfigureAwait(false);
                }
                else
                {
                    await Monitoring.DisableMonitoring(endpointId)
                        .ConfigureAwait(false);
                }

                return HttpStatusCode.Accepted;
            };
        }

        public EndpointInstanceMonitoring Monitoring { get; set; }
    }
}