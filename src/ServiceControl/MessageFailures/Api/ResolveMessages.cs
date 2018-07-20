namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using InternalMessages;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class ResolveMessages : BaseModule
    {
        public ResolveMessages()
        {
            Patch["/pendingretries/resolve", true] = async (_, ctx) =>
            {
                var request = this.Bind<ResolveRequest>();

                if (request.uniquemessageids != null)
                {
                    if (request.uniquemessageids.Any(string.IsNullOrEmpty))
                    {
                        return HttpStatusCode.BadRequest;
                    }

                    foreach (var id in request.uniquemessageids)
                    {
                        await Bus.SendLocal(new MarkPendingRetryAsResolved {FailedMessageId = id})
                            .ConfigureAwait(false);
                    }

                    return HttpStatusCode.Accepted;
                }

                DateTime from, to;

                try
                {
                    from = DateTime.Parse(request.from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    to = DateTime.Parse(request.to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
                catch (Exception)
                {
                    return Negotiate.WithReasonPhrase("From/To").WithStatusCode(HttpStatusCode.BadRequest);
                }

                await Bus.SendLocal<MarkPendingRetriesAsResolved>(m =>
                {
                    m.PeriodFrom = from;
                    m.PeriodTo = to;
                }).ConfigureAwait(false);

                return HttpStatusCode.Accepted;
            };

            Patch["/pendingretries/queues/resolve", true] = async (parameters, ctx) =>
            {
                var request = this.Bind<ResolveRequest>();

                if (string.IsNullOrWhiteSpace(request.queueaddress))
                {
                    return Negotiate.WithReasonPhrase("QueueAddress").WithStatusCode(HttpStatusCode.BadRequest);
                }

                DateTime from, to;

                try
                {
                    from = DateTime.Parse(request.from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    to = DateTime.Parse(request.to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
                catch (Exception)
                {
                    return Negotiate.WithReasonPhrase("From/To").WithStatusCode(HttpStatusCode.BadRequest);
                }

                await Bus.SendLocal<MarkPendingRetriesAsResolved>(m =>
                {
                    m.QueueAddress = request.queueaddress;
                    m.PeriodFrom = from;
                    m.PeriodTo = to;
                }).ConfigureAwait(false);

                return HttpStatusCode.Accepted;
            };
        }

        public IMessageSession Bus { get; set; }

        private class ResolveRequest
        {
            public string queueaddress { get; set; }
            public List<string> uniquemessageids { get; set; }
            public string from { get; set; }
            public string to { get; set; }
        }
    }
}