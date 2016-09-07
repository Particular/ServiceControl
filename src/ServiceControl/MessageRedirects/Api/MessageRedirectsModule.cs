namespace ServiceControl.MessageRedirects.Api
{
    using System;
    using System.Linq;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Contracts.MessageRedirects;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures.InternalMessages;

    public class MessageRedirectsModule : BaseModule
    {
        public IBus Bus { get; set; }

        private class MessageRedirectRequest
        {
            public string fromphysicaladdress { get; set; }
            public string tophysicaladdress { get; set; }
            public bool retryexisting { get; set; }
        }

        public MessageRedirectsModule()
        {
            Post["/redirects"] = parameters =>
            {
                var request = this.Bind<MessageRedirectRequest>();

                if (string.IsNullOrWhiteSpace(request.fromphysicaladdress) || string.IsNullOrWhiteSpace(request.tophysicaladdress))
                {
                    return HttpStatusCode.BadRequest;
                }

                var messageRedirect = new MessageRedirect
                {
                    FromPhysicalAddress = request.fromphysicaladdress,
                    ToPhysicalAddress = request.tophysicaladdress,
                    LastModifiedTicks = DateTime.UtcNow.Ticks
                };

                using (var session = Store.OpenSession())
                {
                    var collection = MessageRedirectsCollection.GetOrCreate(session);

                    var existing = collection[messageRedirect.MessageRedirectId];

                    if (existing != null)
                    {
                        return existing.ToPhysicalAddress == messageRedirect.ToPhysicalAddress
                            ? Negotiate.WithModel(messageRedirect).WithStatusCode(HttpStatusCode.Created)
                            : Negotiate.WithReasonPhrase("Duplicate").WithModel(existing).WithStatusCode(HttpStatusCode.Conflict);
                    }

                    var dependents = collection.Redirects.Where(r => r.ToPhysicalAddress == request.fromphysicaladdress).ToList();

                    if (dependents.Any())
                    {
                        return Negotiate.WithReasonPhrase("Dependents").WithModel(dependents).WithStatusCode(HttpStatusCode.Conflict);
                    }

                    collection.Redirects.Add(messageRedirect);

                    collection.Save(session);
                }

                Bus.Publish(new MessageRedirectCreated
                {
                    MessageRedirectId = messageRedirect.MessageRedirectId,
                    FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                    ToPhysicalAddress = messageRedirect.ToPhysicalAddress
                });

                if (request.retryexisting)
                {
                    Bus.SendLocal(new RetryPendingMessages
                    {
                        QueueAddress = messageRedirect.FromPhysicalAddress,
                        PeriodFrom = DateTime.MinValue,
                        PeriodTo = DateTime.UtcNow
                    });
                }

                return HttpStatusCode.Created;
            };

            Put["/redirects/{messageredirectid:guid}/"] = parameters =>
            {
                Guid messageRedirectId = parameters.messageredirectid;

                var request = this.Bind<MessageRedirectRequest>();

                if (string.IsNullOrWhiteSpace(request.tophysicaladdress))
                {
                    return HttpStatusCode.BadRequest;
                }

                using (var session = Store.OpenSession())
                {
                    var redirects = MessageRedirectsCollection.GetOrCreate(session);

                    var messageRedirect = redirects[messageRedirectId];

                    if (messageRedirect == null)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    var toMessageRedirectId = DeterministicGuid.MakeId(request.tophysicaladdress);

                    if (redirects[toMessageRedirectId] != null)
                    {
                        return HttpStatusCode.Conflict;
                    }

                    var messageRedirectChanged = new MessageRedirectChanged
                    {
                        MessageRedirectId = messageRedirectId,
                        PreviousToPhysicalAddress = messageRedirect.ToPhysicalAddress,
                        FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                        ToPhysicalAddress = messageRedirect.ToPhysicalAddress = request.tophysicaladdress,
                    };

                    messageRedirect.LastModifiedTicks = DateTime.UtcNow.Ticks;

                    redirects.Save(session);

                    Bus.Publish(messageRedirectChanged);

                    return HttpStatusCode.NoContent;
                }
            };

            Delete["/redirects/{messageredirectid:guid}/"] = parameters =>
            {
                Guid messageRedirectId = parameters.messageredirectid;

                using (var session = Store.OpenSession())
                {
                    var redirects = MessageRedirectsCollection.GetOrCreate(session);

                    var messageRedirect = redirects[messageRedirectId];

                    if (messageRedirect == null)
                    {
                        return HttpStatusCode.NoContent;
                    }

                    redirects.Redirects.Remove(messageRedirect);

                    redirects.Save(session);

                    Bus.Publish<MessageRedirectRemoved>(evt =>
                    {
                        evt.MessageRedirectId = messageRedirectId;
                        evt.FromPhysicalAddress = messageRedirect.FromPhysicalAddress;
                        evt.ToPhysicalAddress = messageRedirect.ToPhysicalAddress;
                    });
                }

                return HttpStatusCode.NoContent;
            };

            Head["/redirects"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    var redirects = MessageRedirectsCollection.GetOrCreate(session);

                    return Negotiate
                        .WithEtagAndLastModified(redirects.ETag, redirects.LastModified)
                        .WithTotalCount(redirects.Redirects.Count);
                }
            };

            Get["/redirects"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    var redirects = MessageRedirectsCollection.GetOrCreate(session);

                    var queryResult = redirects
                        .Sort(Request)
                        .Paging(Request)
                        .Select(r => new
                        {
                            r.MessageRedirectId,
                            r.FromPhysicalAddress,
                            r.ToPhysicalAddress,
                            LastModified = new DateTime(r.LastModifiedTicks)
                        });

                    return Negotiate
                        .WithModel(queryResult)
                        .WithEtagAndLastModified(redirects.ETag, redirects.LastModified)
                        .WithPagingLinksAndTotalCount(redirects.Redirects.Count, Request);
                }
            };
        }
    }
}
