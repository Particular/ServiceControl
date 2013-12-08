namespace ServiceControl.MessageAuditing.Api
{
    using System.Linq;
    using Contracts.Operations;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceBus.Management.Infrastructure.RavenDB.Indexes;

    public class AuditMessagesModule : BaseModule
    {
        public AuditMessagesModule()
        {
            Get["/audit"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results = session.Query<Messages_Sort.Result, Messages_Sort>()
                        .Statistics(out stats)
                        .IncludeSystemMessagesWhere(Request)
                        .Where(m => m.Status == MessageStatus.Successful)
                        .Sort(Request)
                        .OfType<AuditMessage>()
                        .Paging(Request)
                        .ToArray();
                    
                    return Negotiate
                        .WithModelAppendedRestfulUrls(results, Request)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

            Get["/endpoints/{name}/audit"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string endpoint = parameters.name;

                    RavenQueryStatistics stats;
                    var results = session.Query<Messages_Sort.Result, Messages_Sort>()
                        .Statistics(out stats)
                        .IncludeSystemMessagesWhere(Request)
                        .Where(
                            m =>
                                m.ReceivingEndpointName == endpoint &&
                                m.Status == MessageStatus.Successful)
                        .Sort(Request)
                        .OfType<AuditMessage>()
                        .Paging(Request)
                        .ToArray();

                    return Negotiate
                        .WithModelAppendedRestfulUrls(results, Request)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }
    }
}