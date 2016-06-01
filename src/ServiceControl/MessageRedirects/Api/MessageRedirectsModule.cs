namespace ServiceControl.MessageRedirects.Api
{
    using System;
    using Microsoft.CSharp.RuntimeBinder;
    using Nancy;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageRedirects.InternalMessages;

    public class MessageRedirectsModule : BaseModule
    {
        public IBus Bus { get; set; }

        public MessageRedirectsModule()
        {
            Post["/redirects"] = parameters =>
            {
                var message = new CreateMessageRedirect();

                DecorateMessage(message, parameters);

                Bus.SendLocal(message);

                return HttpStatusCode.Created;
            };

            Put["/redirects/{id}"] = parameters =>
            {
                var message = new UpdateMessageRedirect();

                DecorateMessage(message, parameters);

                Bus.SendLocal(message);

                return HttpStatusCode.Accepted;
            };

            Delete["/redirects/{id}"] = parameters =>
            {
                Guid id = parameters.id;

                Bus.SendLocal(new EndMessageRedirect
                {
                    MessageRedirectId = id,
                    ExpiresDateTime = DateTime.UtcNow
                });

                return HttpStatusCode.Accepted;
            };

            Head["/redirects"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    var queryResult = session.Advanced
                        .LuceneQuery<MessageRedirectsViewIndex>()
                        .QueryResult;

                    return Negotiate
                        .WithTotalCount(queryResult.TotalResults)
                        .WithEtagAndLastModified(queryResult.IndexEtag, queryResult.IndexTimestamp);
                }
            };
        }

        private static void DecorateMessage(IHaveMessageRedirectData message, dynamic parameters)
        {
            message.MatchMessageType = parameters.matchmessagetype;
            message.MatchSourceEndpoint = parameters.matchsourceendpoint;
            message.RedirectToEndpoint = parameters.redirecttoendpoint;

            Guid id;

            try
            {
                message.MessageRedirectId = parameters.id;
            }
            catch (RuntimeBinderException)
            {
                message.MessageRedirectId = DeterministicGuid.MakeId(message.MatchSourceEndpoint, message.MatchMessageType);
            }

            long asofTicks = 0;

            try
            {
                asofTicks = parameters.asofticks;
            }
            catch (RuntimeBinderException)
            {
                //not always provided
            }

            long expiresTicks = 0;

            try
            {
                expiresTicks = parameters.expiresticks;
            }
            catch (RuntimeBinderException)
            {
                //not always provided
            }

            message.AsOfDateTime = asofTicks == 0 ? DateTime.UtcNow : new DateTime(asofTicks);
            message.ExpiresDateTime = expiresTicks == 0 ? DateTime.MaxValue : new DateTime(expiresTicks);
        }
    }
}
