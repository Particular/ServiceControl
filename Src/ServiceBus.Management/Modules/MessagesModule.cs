namespace ServiceBus.Management.Modules
{
    using System;
    using System.Linq;
    using Extensions;
    using Nancy;
    using Raven.Client;
    using RavenDB.Indexes;


    public class MessagesModule : BaseModule
    {
        public IDocumentStore Store { get; set; }

        public MessagesModule()
        {
            Get["/messages/search/{keyword}"] = parameters =>
            {
                string keyword = parameters.keyword;

                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results = session.Query<Messages_Search.Result, Messages_Search>()
                                         .Statistics(out stats)
                                         .Search(s => s.Query, keyword)
                                         .Sort(Request)
                                         .OfType<Message>()
                                         .Paging(Request)
                                         .ToArray();

                    return Negotiate.WithModel(results)
                                    .WithPagingLinksAndTotalCount(stats, Request)
                                    .WithEtagAndLastModified(stats);
                }
            };

            Get["/endpoints/{name}/messages/search/{keyword}"] = parameters =>
                {
                    string keyword = parameters.keyword;
                    string name = parameters.name;

                    using (var session = Store.OpenSession())
                    {
                        RavenQueryStatistics stats;
                        var results = session.Query<Messages_Search.Result, Messages_Search>()
                                             .Statistics(out stats)
                                             .Search(s => s.Query, keyword)
                                             .Where(m => m.ReceivingEndpointName == name)
                                             .Sort(Request)
                                             .OfType<Message>()
                                             .Paging(Request)
                                             .ToArray();

                        return Negotiate.WithModel(results)
                                        .WithPagingLinksAndTotalCount(stats, Request)
                                        .WithEtagAndLastModified(stats);
                    }
                };

            Get["/endpoints/{name}/messages"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string endpoint = parameters.name;

                    RavenQueryStatistics stats;
                    var results = session.Query<Messages_Sort.Result, Messages_Sort>()
                                         .Statistics(out stats)
                                         .IncludeSystemMessagesWhere(Request)
                                         .Where(m =>m.ReceivingEndpointName == endpoint)
                                         .Sort(Request)
                                         .OfType<Message>()
                                         .Paging(Request)
                                         .ToArray();
                    
                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

            Get["/messages/{id}"] = parameters =>
                {
                    string messageId = parameters.id;

                    using (var session = Store.OpenSession())
                    {
                        var message = session.Load<Message>(messageId);

                        if (message == null)
                        {
                            return HttpStatusCode.NotFound;
                        }

                        var metadata = session.Advanced.GetMetadataFor(message);
                        var etag = metadata.Value<Guid>("@etag");
                        var lastModified = metadata.Value<DateTime>("Last-Modified ");

                        return Negotiate.WithModel(message)
                            .WithHeader("ETag", etag.ToString("N"))
                            .WithHeader("Last-Modified", lastModified.ToString("R"));
                    }
                };
        }
    }
}