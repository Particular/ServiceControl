namespace ServiceControl.MessageRedirects.Api
{
    using System;
    using System.Linq;
    using Nancy;
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

        public MessageRedirectsModule()
        {

            Post["/redirects"] = parameters =>
            {
                var message = new CreateMessageRedirect
                {
                    FromPhysicalAddress = parameters.fromphysicaladdress,
                    ToPhysicalAddress = parameters.tophysicaladdress
                };

                if (string.IsNullOrWhiteSpace(message.FromPhysicalAddress) || string.IsNullOrWhiteSpace(message.ToPhysicalAddress))
                {
                    return HttpStatusCode.BadRequest;
                }

                message.MessageRedirectId = DeterministicGuid.MakeId(message.FromPhysicalAddress, message.ToPhysicalAddress);

                using (var session = Store.OpenSession())
                {
                    var redirect = session.Load<MessageRedirect>(MessageRedirect.GetDocumentIdFromMessageRedirectId(message.MessageRedirectId));

                    if (redirect != null)
                    {
                        return Negotiate.WithReasonPhrase("Duplicate").WithModel(redirect).WithStatusCode(HttpStatusCode.Conflict);
                    }

                    var dependents = session.Query<MessageRedirect>().Where(r => r.ToPhysicalAddress == message.FromPhysicalAddress).ToList();

                    if (dependents.Any())
                    {
                        return Negotiate.WithReasonPhrase("Dependents").WithModel(dependents).WithStatusCode(HttpStatusCode.Conflict);
                    }
                }

                Bus.SendLocal(message);

                return HttpStatusCode.Created;
            };

            Put["/redirects/{messageredirectid}"] = parameters =>
            {
                Guid oldMessageRedirectId = parameters.messageredirectid;

                var message = new CreateMessageRedirect
                {
                    FromPhysicalAddress = parameters.fromphysicaladdress,
                    ToPhysicalAddress = parameters.tophysicaladdress
                };

                if (string.IsNullOrWhiteSpace(message.FromPhysicalAddress) || string.IsNullOrWhiteSpace(message.ToPhysicalAddress))
                {
                    return HttpStatusCode.BadRequest;
                }

                message.MessageRedirectId = DeterministicGuid.MakeId(message.FromPhysicalAddress, message.ToPhysicalAddress);

                Bus.SendLocal(new RemoveMessageRedirect
                {
                    MessageRedirectId = oldMessageRedirectId
                });

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

        private
    }
}
