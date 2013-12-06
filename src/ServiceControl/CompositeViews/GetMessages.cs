namespace ServiceControl.CompositeViews
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using MessageFailures;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceBus.Management.MessageAuditing;
    using ServiceControl.Contracts.Operations;

    public class GetMessages : BaseModule
    {
        public GetMessages()
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
            //            .OfType<Message>()
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
            //            .OfType<Message>()
            //            .Paging(Request)
            //            .ToArray();

            //        return Negotiate.WithModelAppendedRestfulUrls(results, Request)
            //            .WithPagingLinksAndTotalCount(stats, Request)
            //            .WithEtagAndLastModified(stats);
            //    }
            //};

            Get["/messages"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results = session.Query<MessagesView, MessagesViewIndex>()
                        .Statistics(out stats)
                        // .Sort(Request)
                        .Paging(Request)
                        .ToArray();


                    Debug.WriteLine(results);
                    return Negotiate
                        .WithModel(results)
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
                    var results = session.Query<MessagesView, MessagesViewIndex>()
                        .Statistics(out stats)
                        //.IncludeSystemMessagesWhere(Request)
                        .Where(m => m.ReceivingEndpointName == endpoint)
                        //.Sort(Request)
                        .Paging(Request)
                        .ToArray();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

            //Get["/messages/{id}"] = parameters =>
            //{
            //    string messageId = parameters.id;

            //    using (var session = Store.OpenSession())
            //    {
            //        var message = session.Load<Message>(messageId);

            //        if (message == null)
            //        {
            //            return HttpStatusCode.NotFound;
            //        }

            //        var metadata = session.Advanced.GetMetadataFor(message);
            //        var etag = metadata.Value<Guid>("@etag");
            //        var lastModified = metadata.Value<DateTime>("Last-Modified");

            //        message.Url = BaseUrl + "/messages/" + message.Id;

            //        if (!String.IsNullOrEmpty(message.ConversationId))
            //        {
            //            message.ConversationUrl = BaseUrl + "/conversations/" + message.ConversationId;
            //        }

            //        if (message.Status == MessageStatus.Failed || message.Status == MessageStatus.RepeatedFailure)
            //        {
            //            message.RetryUrl = BaseUrl + "/errors/" + message.Id + "/retry";
            //        }

            //        return Negotiate.WithModel(message)
            //            .WithHeader("ETag", etag.ToString("N"))
            //            .WithHeader("Last-Modified", lastModified.ToString("R"));
            //    }
            //};
        }

        public IDocumentStore Store { get; set; }
    }

    public class MessagesView
    {
        public string MessageId { get; set; }
        public MessageStatus Status { get; set; }

        public DateTime ProcessedAt { get; set; }

        public string ReceivingEndpointName { get; set; }
    }

    public class MessagesViewIndex : AbstractMultiMapIndexCreationTask<MessagesView>
    {
        public MessagesViewIndex()
        {
            AddMap<Message>(messages => messages.Select(message => new
            {
                MessageId = message.MessageId,
                message.Status,
                ProcessedAt = new DateTime(2013,12,6),
                ReceivingEndpointName = message.ReceivingEndpoint.Name
            }));


            AddMap<FailedMessage>(messages => messages.Select(message => new
            {
                MessageId =message.MessageId,
                message.Status,
                ProcessedAt = new DateTime(2013, 12, 7),
                ReceivingEndpointName = message.ProcessingAttempts.Last().Message.ProcessingEndpoint.Name
            }));

            Reduce = results => from message in results
                                group message by message.MessageId
                                    into g
                                    select new MessagesView
                                    {
                                        MessageId = g.Key,
                                        Status = g.OrderByDescending(m=>m.ProcessedAt).First().Status,
                                        ProcessedAt = g.OrderByDescending(m => m.ProcessedAt).First().ProcessedAt,
                                        ReceivingEndpointName = g.OrderByDescending(m => m.ProcessedAt).First().ReceivingEndpointName,
                                    };

          

            ////Index(x => x.Query, FieldIndexing.Analyzed);
            //Index(x => x.ReceivingEndpointName, FieldIndexing.Default);
            //Index(x => x.CriticalTime, FieldIndexing.Default);
            //Index(x => x.ProcessingTime, FieldIndexing.Default);

            //Sort(x => x.CriticalTime, SortOptions.Long);
            //Sort(x => x.ProcessingTime, SortOptions.Long);

            //Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);
        }

        //public class Result : CommonResult
        //{
        //    public string Query { get; set; }
        //}
    }
}