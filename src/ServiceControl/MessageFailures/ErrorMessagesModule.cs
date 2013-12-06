namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using InternalMessages;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class ErrorMessagesModule : BaseModule
    {
        public ErrorMessagesModule()
        {

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

        public IBus Bus { get; set; }
    }


}