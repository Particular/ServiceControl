namespace ServiceBus.Management.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Infrastructure.Extensions;
    using Infrastructure.Nancy.Modules;
    using Infrastructure.RavenDB.Indexes;
    using InternalMessages;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Indexing;
    using Raven.Client;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class ErrorMessagesModule : BaseModule
    {
        public ErrorMessagesModule()
        {
            //Get["/errors"] = _ =>
            //{
            //    using (var session = Store.OpenSession())
            //    {
            //        RavenQueryStatistics stats;

            //        var results = session.Load<FailedMessageView, FaileMessageViewTransformer>()
            //      //   .TransformWith<FaileMessageViewTransformer, FailedMessageView>()
            //            .Statistics(out stats)
            //            .Where(m =>
            //                m.Status != MessageStatus.SuccessfullyRetried &&
            //                m.Status != MessageStatus.RetryIssued)
            //            .Sort(Request)
            //            .Paging(Request)
            //            .ToArray();

            //        return Negotiate
            //            .WithModel(results)
            //            .WithPagingLinksAndTotalCount(stats, Request)
            //            .WithEtagAndLastModified(stats);
            //    }
            //};

            //Get["/errors/facets"] = _ =>
            //{
            //    using (var session = Store.OpenSession())
            //    {
            //        var facetResults = session.Query<FailedMessageView>()
            //         //.TransformWith<FaileMessageViewTransformer, FailedMessageView>()
            //            .ToFacets("facets/messageFailureFacets")
            //            .Results;

            //        return Negotiate.WithModel(facetResults);
            //    }
            //};

            //Get["/endpoints/{name}/errors"] = parameters =>
            //{
            //    using (var session = Store.OpenSession())
            //    {
            //        string endpoint = parameters.name;

            //        RavenQueryStatistics stats;
            //        var results = session.Query<FailedMessageView>()
            //         //.TransformWith<FaileMessageViewTransformer, FailedMessageView>()
            //            .Statistics(out stats)
            //            .Where(m =>
            //                m.ReceivingEndpointName == endpoint &&
            //                m.Status != MessageStatus.SuccessfullyRetried &&
            //                m.Status != MessageStatus.RetryIssued)
            //            .Sort(Request)
            //            .Paging(Request)
            //            .ToArray();

            //        return Negotiate
            //            .WithModel(results)
            //            .WithPagingLinksAndTotalCount(stats, Request)
            //            .WithEtagAndLastModified(stats);
            //    }
            //};

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