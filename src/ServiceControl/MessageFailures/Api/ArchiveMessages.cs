namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using InternalMessages;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class ArchiveMessages : BaseModule
    {
        public IBusSession BusSession { get; set; }

        public ArchiveMessages()
        {
            Patch["/errors/archive", true] = async (_, ct) =>
            {
                var ids = this.Bind<List<string>>();

                if (ids.Any(string.IsNullOrEmpty))
                {
                    return HttpStatusCode.BadRequest;
                }

                foreach (var id in ids)
                {
                    var request = new ArchiveMessage { FailedMessageId = id };

                    await BusSession.SendLocal(request); 
                }

                return HttpStatusCode.Accepted;
            };

            Patch["/errors/{messageid}/archive", true] = async (parameters, ct) =>
            {
                var failedMessageId = parameters.MessageId;

                if (string.IsNullOrEmpty(failedMessageId))
                {
                    return HttpStatusCode.BadRequest;
                }

                await BusSession.SendLocal<ArchiveMessage>(m =>
                {
                    m.FailedMessageId = failedMessageId;
                });

                return HttpStatusCode.Accepted;
            };
        }
    }
}