namespace ServiceBus.Management.Modules
{
    using System;
    using System.Globalization;
    using System.Linq;
    using Commands;
    using Extensions;
    using NServiceBus;
    using Nancy;
    using Nancy.ModelBinding;
    using Raven.Client;
    using RavenDB.Indexes;

    public class ErrorMessagesModule : BaseModule
    {
        public IDocumentStore Store { get; set; }

        public IBus Bus { get; set; }

        public ErrorMessagesModule()
        {
            Get["/errors"] = _ =>
                {
                    using (var session = Store.OpenSession())
                    {
                        RavenQueryStatistics stats;
                        var results = session.Query<Messages_Sort.Result, Messages_Sort>()
                            .Statistics(out stats)
                            .Where(m => 
                                m.Status != MessageStatus.Successful &&
                                m.Status != MessageStatus.RetryIssued)
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

            Get["/endpoints/{name}/errors"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string endpoint = parameters.name;

                    RavenQueryStatistics stats;
                    var results = session.Query<Messages_Sort.Result, Messages_Sort>()
                        .Statistics(out stats)
                        .Where(m => 
                            m.ReceivingEndpointName == endpoint &&  
                            m.Status != MessageStatus.Successful && 
                            m.Status != MessageStatus.RetryIssued)
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

            Post["/errors/{messageid}/retry"] = parameters =>
                {
                    var request = this.Bind<IssueRetry>();

                    request.SetHeader("RequestedAt", DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));

                    Bus.SendLocal(request);

                    return HttpStatusCode.Accepted;
                };

        }
    }
}