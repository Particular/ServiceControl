namespace ServiceControl.MessageRedirects.Api
{
    using System;
    using System.Linq;
    using Contracts.MessageRedirects;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using MessageFailures.InternalMessages;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class MessageRedirectsModule : BaseModule
    {
        public MessageRedirectsModule()
        {
            Post["/redirects", true] = async (parameters, token) =>
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

                using (var session = Store.OpenAsyncSession())
                {
                    var collection = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);

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

                    await collection.Save(session).ConfigureAwait(false);
                }

                await DomainEvents.Raise(new MessageRedirectCreated
                {
                    MessageRedirectId = messageRedirect.MessageRedirectId,
                    FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                    ToPhysicalAddress = messageRedirect.ToPhysicalAddress
                }).ConfigureAwait(false);

                if (request.retryexisting)
                {
                    await Bus.SendLocal(new RetryPendingMessages
                    {
                        QueueAddress = messageRedirect.FromPhysicalAddress,
                        PeriodFrom = DateTime.MinValue,
                        PeriodTo = DateTime.UtcNow
                    }).ConfigureAwait(false);
                }

                return HttpStatusCode.Created;
            };

            Put["/redirects/{messageredirectid:guid}/", true] = async (parameters, token) =>
            {
                Guid messageRedirectId = parameters.messageredirectid;

                var request = this.Bind<MessageRedirectRequest>();

                if (string.IsNullOrWhiteSpace(request.tophysicaladdress))
                {
                    return HttpStatusCode.BadRequest;
                }

                using (var session = Store.OpenAsyncSession())
                {
                    var redirects = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);

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
                        ToPhysicalAddress = messageRedirect.ToPhysicalAddress = request.tophysicaladdress
                    };

                    messageRedirect.LastModifiedTicks = DateTime.UtcNow.Ticks;

                    await redirects.Save(session).ConfigureAwait(false);

                    await DomainEvents.Raise(messageRedirectChanged)
                        .ConfigureAwait(false);

                    return HttpStatusCode.NoContent;
                }
            };

            Delete["/redirects/{messageredirectid:guid}/", true] = async (parameters, token) =>
            {
                Guid messageRedirectId = parameters.messageredirectid;

                using (var session = Store.OpenAsyncSession())
                {
                    var redirects = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);

                    var messageRedirect = redirects[messageRedirectId];

                    if (messageRedirect == null)
                    {
                        return HttpStatusCode.NoContent;
                    }

                    redirects.Redirects.Remove(messageRedirect);

                    await redirects.Save(session).ConfigureAwait(false);

                    await DomainEvents.Raise(new MessageRedirectRemoved
                    {
                        MessageRedirectId = messageRedirectId,
                        FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                        ToPhysicalAddress = messageRedirect.ToPhysicalAddress
                    }).ConfigureAwait(false);
                }

                return HttpStatusCode.NoContent;
            };

            Head["/redirects", true] = async (parameters, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    var redirects = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);

                    return Negotiate
                        .WithEtag(redirects.ETag)
                        .WithTotalCount(redirects.Redirects.Count);
                }
            };

            Get["/redirects", true] = async (parameters, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    var redirects = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);

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
                        .WithEtag(redirects.ETag)
                        .WithPagingLinksAndTotalCount(redirects.Redirects.Count, Request);
                }
            };
        }

        public IMessageSession Bus { get; set; }
        public IDomainEvents DomainEvents { get; set; }

        private class MessageRedirectRequest
        {
            public string fromphysicaladdress { get; set; }
            public string tophysicaladdress { get; set; }
            public bool retryexisting { get; set; }
        }
    }
}