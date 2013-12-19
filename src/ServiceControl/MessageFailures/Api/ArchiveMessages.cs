namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using InternalMessages;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class ArchiveMessages : BaseModule
    {
        public IBus Bus { get; set; }

        public ArchiveMessages()
        {
            Post["/errors/archive"] = _ =>
            {
                var ids = this.Bind<List<string>>();

                if (ids.Any(string.IsNullOrEmpty))
                {
                    return HttpStatusCode.BadRequest;
                }

                var requestedAt = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);

                foreach (var id in ids)
                {
                    var request = new ArchiveMessage { FailedMessageId = id };

                    request.SetHeader("RequestedAt", requestedAt);

                    Bus.SendLocal(request); 
                }

                return HttpStatusCode.Accepted;
            };

            Post["/errors/{messageid}/archive"] = parameters =>
            {
                var failedMessageId = parameters.MessageId;

                if (string.IsNullOrEmpty(failedMessageId))
                {
                    return HttpStatusCode.BadRequest;
                }

                Bus.SendLocal<ArchiveMessage>(m =>
                {
                    m.SetHeader("RequestedAt", DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow));
                    m.FailedMessageId = failedMessageId;
                });

                return HttpStatusCode.Accepted;
            };
        }
    }
}