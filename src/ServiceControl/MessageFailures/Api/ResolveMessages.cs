namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.MessageFailures.InternalMessages;

    public class ResolveMessages : BaseModule
    {
        public IBus Bus { get; set; }

        private class ResolveRequest
        {
            public string queueaddress { get; set; }
            public List<string> uniquemessageids { get; set; }
            public string from { get; set; }
            public string to { get; set; }
        }

        public ResolveMessages()
        {
            Patch["/pendingretries/resolve"] = _ =>
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
                        Bus.SendLocal(new MarkPendingRetryAsResolved { FailedMessageId = id });
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

                Bus.SendLocal<MarkPendingRetriesAsResolved>(m =>
                {
                    m.PeriodFrom = from;
                    m.PeriodTo = to;
                });

                return HttpStatusCode.Accepted;
            };

            Patch["/pendingretries/queues/resolve"] = parameters =>
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

                Bus.SendLocal<MarkPendingRetriesAsResolved>(m =>
                {
                    m.QueueAddress = request.queueaddress;
                    m.PeriodFrom = from;
                    m.PeriodTo = to;
                });

                return HttpStatusCode.Accepted;
            };
        }
    }
}