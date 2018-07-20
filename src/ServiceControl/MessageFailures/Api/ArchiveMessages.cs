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
        public ArchiveMessages()
        {
            Post["/errors/archive", true] = Patch["/errors/archive", true] = async (_, ctx) =>
            {
                var ids = this.Bind<List<string>>();

                if (ids.Any(string.IsNullOrEmpty))
                {
                    return HttpStatusCode.BadRequest;
                }

                foreach (var id in ids)
                {
                    var request = new ArchiveMessage {FailedMessageId = id};

                    await Bus.SendLocal(request).ConfigureAwait(false);
                }

                return HttpStatusCode.Accepted;
            };

            Post["/errors/{messageid}/archive", true] = Patch["/errors/{messageid}/archive", true] = async (parameters, ctx) =>
            {
                var failedMessageId = parameters.MessageId;

                if (string.IsNullOrEmpty(failedMessageId))
                {
                    return HttpStatusCode.BadRequest;
                }

                await Bus.SendLocal<ArchiveMessage>(m => { m.FailedMessageId = failedMessageId; }).ConfigureAwait(false);

                return HttpStatusCode.Accepted;
            };
        }

        public IMessageSession Bus { get; set; }
    }
}