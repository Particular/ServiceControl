namespace ServiceControl.CompositeViews.Endpoints
{
    using System;
    using System.Collections.Generic;
    using EndpointControl;
    using HeartbeatMonitoring;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using Operations;
    using Raven.Abstractions.Data;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class EndpointUpdateModel : ICommand
    {
        public bool MonitorHeartbeat { get; set; }
    }

    public class GetEndpoints : BaseModule
    {
        public IBusSession BusSession { get; set; }

        public LicenseStatusKeeper LicenseStatusKeeper { get; set; }

        public GetEndpoints()
        {
            Patch["/endpoints/{id}", true] = async (parameters, ct) =>
            {
                var data = this.Bind<EndpointUpdateModel>();
                var endpointId = (Guid) parameters.id;

                using (var session = Store.OpenSession())
                {
                    var endpoint = session.Load<KnownEndpoint>(endpointId);

                    if (endpoint == null)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    if (data.MonitorHeartbeat == endpoint.Monitored)
                    {
                        return HttpStatusCode.NotModified;
                    }

                    if (data.MonitorHeartbeat)
                    {
                        await BusSession.SendLocal(new EnableEndpointMonitoring{EndpointId = endpointId});
                    }
                    else
                    {
                        await BusSession.SendLocal(new DisableEndpointMonitoring { EndpointId = endpointId });
                    }
                }

             

                return HttpStatusCode.Accepted;
            };

            Get["/endpoints"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    QueryHeaderInformation stats;

                    var query = session.Query<KnownEndpoint, KnownEndpointIndex>();

                    var results = new List<EndpointsView>();
                    
                    using (var ie = session.Advanced.Stream(query, out stats))
                    {
                        while (ie.MoveNext())
                        {
                            var knownEndpoint = ie.Current.Document;
                            var view = new EndpointsView
                            {
                                Id = knownEndpoint.Id,
                                Name = knownEndpoint.EndpointDetails.Name,
                                HostDisplayName = knownEndpoint.HostDisplayName,
                                Monitored = knownEndpoint.Monitored,
                                MonitorHeartbeat = knownEndpoint.Monitored,
                                LicenseStatus = LicenseStatusKeeper.Get(knownEndpoint.EndpointDetails.Name + knownEndpoint.HostDisplayName)
                            };

                            session.Advanced.Lazily.Load<Heartbeat>(knownEndpoint.Id,
                                heartbeat =>
                                {
                                    if (heartbeat == null)
                                    {
                                        return;
                                    }

                                    view.IsSendingHeartbeats = true;

                                    view.MonitorHeartbeat = !heartbeat.Disabled;

                                    view.HeartbeatInformation =
                                        new HeartbeatInformation
                                        {
                                            LastReportAt = heartbeat.LastReportAt,
                                            ReportedStatus = heartbeat.ReportedStatus
                                        };
                                });

                            results.Add(view);
                        }

                        session.Advanced.Eagerly.ExecuteAllPendingLazyOperations();
                    }

                    return Negotiate.WithModel(results)
                        .WithEtagAndLastModified(stats);
                }
            };
        }
    }
}