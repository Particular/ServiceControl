namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.ScaleOut;

    public class ListScaleOutGroups : BaseModule
    {
        public ListScaleOutGroups()
        {
            Get["/scaleoutgroups"] = parameters =>
            {
                object results;
                RavenQueryStatistics stats;
                var settings = new ScaleOutGroupSettings(String.Empty);

                using (var session = Store.OpenSession())
                {
                    results = session.Query<ListScaleOutGroupsIndex.Result, ListScaleOutGroupsIndex>()
                        .TransformWith<ListScaleOutGroupsTransformer, ScaleOutGroup>()
                        .Statistics(out stats)
                        .Take(1024)
                        .ToList()
                        .Select(r =>
                        {
                            r.Settings = r.Settings ?? settings;

                            return new
                            {
                                r.Name,
                                Settings = new
                                {
                                    r.Settings.ConnectAutomatically,
                                    r.Settings.MinimumConnected,
                                    r.Settings.ReconnectAutomatically
                                }
                            };
                        })
                        .ToArray();
                }

                return Negotiate.WithModel(results)
                      .WithEtagAndLastModified(stats);
            };
        }
    }
}