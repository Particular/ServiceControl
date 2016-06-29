namespace ServiceControl.MessageRedirects.Api
{
    using System;
    using System.Linq;
    using Nancy;
    using Nancy.ModelBinding;
    using Nancy.Responses.Negotiation;
    using NServiceBus;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.Extensions;
    using ServiceControl.MessageRedirects.InternalMessages;

    public class MessageRedirectsModule : BaseModule
    {
        public IBus Bus { get; set; }

        private class MessageRedirectRequest
        {
            public string fromphysicaladdress { get; set; }
            public string tophysicaladdress { get; set; }
        }

        private MessageRedirectRequest BindAndNormalize()
        {
            var request = this.Bind<MessageRedirectRequest>();

            request.fromphysicaladdress = request.fromphysicaladdress.ToLowerInvariant();
            request.tophysicaladdress = request.tophysicaladdress.ToLowerInvariant();

            return request;
        }

        public MessageRedirectsModule()
        {
            Post["/redirects"] = parameters =>
            {
                var request = BindAndNormalize();

                var result = ValidateRedirectRequest(request);

                if (result.NegotiationContext.StatusCode != HttpStatusCode.OK)
                {
                    return result;
                }

                Bus.SendLocal(new CreateMessageRedirect
                {
                    FromPhysicalAddress = request.fromphysicaladdress,
                    ToPhysicalAddress = request.tophysicaladdress,
                    MessageRedirectId = DeterministicGuid.MakeId(request.fromphysicaladdress)
                });

                return HttpStatusCode.Created;
            };

            Put["/redirects/{messageredirectid:guid}"] = parameters =>
            {
                Guid messageRedirectId = parameters.messageredirectid;

                var request = BindAndNormalize();

                var result = ValidateRedirectRequest(request, false);

                if (result.NegotiationContext.StatusCode != HttpStatusCode.OK)
                {
                    return result;
                }

                var message = new ChangeMessageRedirect
                {
                    ToPhysicalAddress = request.tophysicaladdress,
                    MessageRedirectId = messageRedirectId
                };

                Bus.SendLocal(message);

                return Negotiate.WithStatusCode(HttpStatusCode.Accepted);
            };

            Delete["/redirects/{messageredirectid}"] = parameters =>
            {

                Guid messageRedirectId = parameters.messageredirectid;

                Bus.SendLocal(new RemoveMessageRedirect
                {
                    MessageRedirectId = messageRedirectId
                });

                return HttpStatusCode.Accepted;
            };

            Head["/redirects"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;

                    session
                        .Query<MessageRedirect>()
                        .Statistics(out stats);

                    return Negotiate
                        .WithTotalCount(stats)
                        .WithEtagAndLastModified(stats);
                }
            };

            Get["/redirects"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;

                    var queryResult = session.Query<MessageRedirect>()
                        .Statistics(out stats)
                        .Sort(Request)
                        .Paging(Request)
                        .ToArray()
                        .Select(r => new
                        {
                            MessageRedirectId = MessageRedirect.GetMessageRedirectIdFromDocumentId(r.Id),
                            r.FromPhysicalAddress,
                            r.ToPhysicalAddress,
                            LastModified = session.Advanced.GetMetadataFor(r)["Last-Modified"].Value<DateTime>()
                        });

                    return Negotiate
                        .WithModel(queryResult)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }

        private Negotiator ValidateRedirectRequest(MessageRedirectRequest request, bool checkForExisting = true)
        {
            if (string.IsNullOrWhiteSpace(request?.fromphysicaladdress) || string.IsNullOrWhiteSpace(request.tophysicaladdress))
            {
                return Negotiate.WithStatusCode(HttpStatusCode.BadRequest);
            }

            using (var session = Store.OpenSession())
            {
                if (checkForExisting)
                {
                    var existing = session.Query<MessageRedirect>().SingleOrDefault(r => r.FromPhysicalAddress == request.fromphysicaladdress);

                    if (existing != null && existing.ToPhysicalAddress != request.tophysicaladdress)
                    {
                        return Negotiate.WithReasonPhrase("Duplicate").WithModel(existing).WithStatusCode(HttpStatusCode.Conflict);
                    }
                }

                var dependents = session.Query<MessageRedirect>().Where(r => r.ToPhysicalAddress == request.fromphysicaladdress).ToList();

                if (dependents.Any())
                {
                    return Negotiate.WithReasonPhrase("Dependents").WithModel(dependents).WithStatusCode(HttpStatusCode.Conflict);
                }
            }

            return Negotiate.WithStatusCode(HttpStatusCode.OK);
        }
    }
}
