namespace ServiceControl.CompositeViews
{
    using System.Diagnostics;
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetMessagesByQuery : BaseModule
    {
        public GetMessagesByQuery()
        {
            //Get["/messages/search/{keyword}"] = parameters =>
            //{
            //    string keyword = parameters.keyword;

            //    using (var session = Store.OpenSession())
            //    {
            //        RavenQueryStatistics stats;
            //        var results = session.Query<MessagesViewIndex.Result, MessagesViewIndex>()
            //            .Statistics(out stats)
            //            .Search(s => s.Query, keyword)
            //            .Sort(Request)
            //            .OfType<AuditMessage>()
            //            .Paging(Request)
            //            .ToArray();

            //        return Negotiate.WithModelAppendedRestfulUrls(results, Request)
            //            .WithPagingLinksAndTotalCount(stats, Request)
            //            .WithEtagAndLastModified(stats);
            //    }
            //};

            //Get["/endpoints/{name}/messages/search/{keyword}"] = parameters =>
            //{
            //    string keyword = parameters.keyword;
            //    string name = parameters.name;

            //    using (var session = Store.OpenSession())
            //    {
            //        RavenQueryStatistics stats;
            //        var results = session.Query<MessagesViewIndex.Result, MessagesViewIndex>()
            //            .Statistics(out stats)
            //            .Search(s => s.Query, keyword)
            //            .Where(m => m.ReceivingEndpointName == name)
            //            .Sort(Request)
            //            .OfType<AuditMessage>()
            //            .Paging(Request)
            //            .ToArray();

            //        return Negotiate.WithModelAppendedRestfulUrls(results, Request)
            //            .WithPagingLinksAndTotalCount(stats, Request)
            //            .WithEtagAndLastModified(stats);
            //    }
            //};
        }

    }
}