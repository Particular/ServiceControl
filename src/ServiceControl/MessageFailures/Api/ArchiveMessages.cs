namespace ServiceControl.MessageFailures.Api
{
    using System;
    using InternalMessages;
    using Nancy;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class ArchiveMessages : BaseModule
    {
        public IBus Bus { get; set; }

        public ArchiveMessages()
        {

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