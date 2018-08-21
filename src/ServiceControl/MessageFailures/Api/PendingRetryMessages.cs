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

    public class PendingRetryMessages : BaseModule
    {
        public PendingRetryMessages()
        {
            Post["/pendingretries/retry", true] = async (_, ctx) =>
            {
                var ids = this.Bind<List<string>>();

                if (ids.Any(string.IsNullOrEmpty))
                {
                    return HttpStatusCode.BadRequest;
                }

                await Bus.SendLocal<RetryPendingMessagesById>(m => m.MessageUniqueIds = ids.ToArray())
                    .ConfigureAwait(false);

                return HttpStatusCode.Accepted;
            };

            Post["/pendingretries/queues/retry", true] = async (parameters, ctx) =>
            {
                var request = this.Bind<PendingRetryRequest>();

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

                await Bus.SendLocal<RetryPendingMessages>(m =>
                {
                    m.QueueAddress = request.queueaddress;
                    m.PeriodFrom = from;
                    m.PeriodTo = to;
                }).ConfigureAwait(false);

                return HttpStatusCode.Accepted;
            };
        }

        public IMessageSession Bus { get; set; }

        private class PendingRetryRequest
        {
            public string queueaddress { get; set; }
            public string from { get; set; }
            public string to { get; set; }
        }
    }
}