namespace ServiceBus.Management.Modules
{
    using System.Linq;
    using Extensions;
    using Nancy;
    using Raven.Client;
    using RavenDB.Indexes;

    public class AuditMessagesModule : BaseModule
    {
        public IDocumentStore Store { get; set; }

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
                                             .OfType<Message>()
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
                                             .OfType<Message>()
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