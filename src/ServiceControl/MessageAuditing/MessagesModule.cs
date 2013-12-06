namespace ServiceBus.Management.MessageAuditing
{
    using System;
    using System.Linq;
    using Infrastructure.Extensions;
    using Infrastructure.Nancy.Modules;
    using Infrastructure.RavenDB.Indexes;
    using Nancy;
    using Raven.Client;
    using ServiceControl.Contracts.Operations;

    public class MessagesModule : BaseModule
    {
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

                    return Negotiate.WithModelAppendedRestfulUrls(results, Request)
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

                    return Negotiate.WithModelAppendedRestfulUrls(results, Request)
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
                        .Where(m => m.ReceivingEndpointName == endpoint)
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
                    var lastModified = metadata.Value<DateTime>("Last-Modified");

                    message.Url = BaseUrl + "/messages/" + message.Id;

                    if (!String.IsNullOrEmpty(message.ConversationId))
                    {
                        message.ConversationUrl = BaseUrl + "/conversations/" + message.ConversationId;
                    }

                    if (message.Status == MessageStatus.Failed || message.Status == MessageStatus.RepeatedFailure)
                    {
                        message.RetryUrl = BaseUrl + "/errors/" + message.Id + "/retry";
                    }

                    return Negotiate.WithModel(message)
                        .WithHeader("ETag", etag.ToString("N"))
                        .WithHeader("Last-Modified", lastModified.ToString("R"));
                }
            };
        }

        public IDocumentStore Store { get; set; }
    }
}