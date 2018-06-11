namespace ServiceControl.Monitoring
{
    using System;
    using Nancy;
    using Nancy.ModelBinding;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.CompositeViews.Messages;

    public class EndpointUpdateModel
    {
        public bool MonitorHeartbeat { get; set; }
    }

    public class ApiModule : BaseModule
    {
        public EndpointInstanceMonitoring Monitoring { get; set; }
        public GetKnownEndpointsApi KnownEndpointsApi { get; set; }

        public ApiModule()
        {
            Get["/heartbeats/stats"] = _ => Negotiate.WithModel(Monitoring.GetStats());

            Get["/endpoints"] = _ => Negotiate.WithModel(Monitoring.GetEndpoints());

            Get["/endpoints/known", true] = (_, token) => KnownEndpointsApi.Execute(this);

            Patch["/endpoints/{id}", true] = async (parameters, token) =>
            {
                var data = this.Bind<EndpointUpdateModel>();
                var endpointId = (Guid) parameters.id;

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
    }
}