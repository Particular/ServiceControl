namespace ServiceBus.Management.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Commands;
    using Extensions;
    using global::Nancy;
    using global::Nancy.ModelBinding;
    using NServiceBus;
    using Raven.Client;
    using RavenDB.Indexes;

    public class ErrorMessagesModule : BaseModule
    {
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

            Post["/errors/{messageid}/retry"] = _ =>
            {
                var request = this.Bind<IssueRetry>();

                request.SetHeader("RequestedAt", DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow));

                Bus.SendLocal(request);

                return HttpStatusCode.Accepted;
            };

            Post["/errors/retry"] = _ =>
            {
                var ids = this.Bind<List<string>>();

                var request = new IssueRetries {MessageIds = ids};
                request.SetHeader("RequestedAt", DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow));

                Bus.SendLocal(request);

                return HttpStatusCode.Accepted;
            };

            Post["/errors/retry/all"] = _ =>
            {
                var request = new IssueRetryAll();
                request.SetHeader("RequestedAt", DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow));

                Bus.SendLocal(request);

                return HttpStatusCode.Accepted;
            };

            Post["/errors/{name}/retry/all"] = parameters =>
            {
                var request = new IssueEndpointRetryAll {EndpointName = parameters.name};
                request.SetHeader("RequestedAt", DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow));

                Bus.SendLocal(request);

                return HttpStatusCode.Accepted;
            };
        }

        public IDocumentStore Store { get; set; }

        public IBus Bus { get; set; }
    }
}