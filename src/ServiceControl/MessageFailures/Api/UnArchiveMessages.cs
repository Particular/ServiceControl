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

    public class UnArchiveMessages : BaseModule
    {
        public IMessageSession MessageSession { get; set; }

        public UnArchiveMessages()
        {
            Patch["/errors/unarchive", true] = async (_, token) =>
            {
                var ids = this.Bind<List<string>>();

                if (ids.Any(string.IsNullOrEmpty))
                {
                    return HttpStatusCode.BadRequest;
                }

                var request = new InternalMessages.UnArchiveMessages { FailedMessageIds = ids };

                await MessageSession.SendLocal(request);

                return HttpStatusCode.Accepted;
            };

            Patch["/errors/{from}...{to}/unarchive", true] = async (parameters, token) =>
            {
                DateTime from, to;

                try
                {
                    from = DateTime.Parse(parameters.from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    to = DateTime.Parse(parameters.to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
                catch (Exception)
                {
                    return HttpStatusCode.BadRequest;
                }

                await MessageSession.SendLocal(new UnArchiveMessagesByRange
                {
                    From = from,
                    To = to,
                    CutOff = DateTime.UtcNow
                });

                return HttpStatusCode.Accepted;
            };
        }
    }
}