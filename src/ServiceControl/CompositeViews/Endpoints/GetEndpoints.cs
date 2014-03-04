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

    public class GetEndpoints : BaseModule
    {
        public IBus Bus { get; set; }

        public LicenseStatusKeeper LicenseStatusKeeper { get; set; }

        public GetEndpoints()
        {
            Patch["/endpoints/{id}"] = parameters =>
            {
                var data = this.Bind<KnownEndpointUpdate>();
                data.KnownEndpointId = (Guid) parameters.id;

                Bus.SendLocal(data);

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
                                Name = knownEndpoint.Name,
                                HostDisplayName = knownEndpoint.HostDisplayName,
                                MonitorHeartbeat = knownEndpoint.MonitorHeartbeat,
                                LicenseStatus = LicenseStatusKeeper.Get(knownEndpoint.Name + knownEndpoint.HostDisplayName)
                            };

                            session.Advanced.Lazily.Load<Heartbeat>(knownEndpoint.Id,
                                heartbeat =>
                                {
                                    if (heartbeat == null)
                                    {
                                        return;
                                    }

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