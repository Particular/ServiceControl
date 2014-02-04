namespace ServiceControl.CompositeViews.Endpoints
{
    using System;
    using System.Collections.Generic;
    using EndpointControl;
    using HeartbeatMonitoring;
    using Nancy;
    using Raven.Abstractions.Data;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetEndpoints : BaseModule
    {
        public GetEndpoints()
        {
            Get["/endpoints"] = parameters =>
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
                                Name = knownEndpoint.Name,
                                HostDisplayName = knownEndpoint.HostDisplayName,
                                MonitorHeartbeat = knownEndpoint.MonitorHeartbeat,
                            };

                            session.Advanced.Lazily.Load<Heartbeat>(knownEndpoint.Id,
                                heartbeat =>
                                {
                                    if (heartbeat == null)
                                    {
                                        return;
                                    }

                                    view.HeartbeatInformation =
                                        new HeartbeatInformation()
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

    public class EndpointsView
    {
        public string Name { get; set; }
        public string HostDisplayName { get; set; }
        public bool MonitorHeartbeat { get; set; }
        public HeartbeatInformation HeartbeatInformation { get; set; }
    }

    public class HeartbeatInformation
    {
        public DateTime LastReportAt { get; set; }
        public Status ReportedStatus { get; set; }
    }
}