namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetScaleOutGroup : BaseModule
    {
        public GetScaleOutGroup()
        {
            Get["/scaleoutgroup/{id}"] = parameters =>
            {
                string groupId = parameters.id;

                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var availableRoutes = session.Query<ScaleOutGroupRegistration>()
                        .Where(r => r.GroupId == groupId)
                        .Statistics(out stats)
                        .Take(1024)
                        .Select(r=> new
                        {
                            r.Address,
                            r.Status
                        })
                        .ToArray();

                    if (availableRoutes.Length == 0)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    return Negotiate.WithModel(availableRoutes)
                        .WithEtagAndLastModified(stats);
                }
            };
        }
    }
}