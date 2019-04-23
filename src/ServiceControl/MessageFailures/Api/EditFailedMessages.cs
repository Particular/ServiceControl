namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.IO;
    using System.Text;
    using Nancy;
    using NServiceBus;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    class EditFailedMessages : BaseModule
    {
        public EditFailedMessages()
        {
            Post["/edit/{messageid}", true] = async (parameters, token) =>
            {
                string failedMessageId = parameters.MessageId;

                if (string.IsNullOrEmpty(failedMessageId))
                {
                    return HttpStatusCode.BadRequest;
                }

                //TODO: consider sending base64 encoded body from the client
                string body;
                using (var streamReader = new StreamReader(this.Request.Body))
                {
                    body = await streamReader.ReadToEndAsync().ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        return HttpStatusCode.BadRequest;
                    }
                }

                var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(body));
                await Bus.SendLocal(new EditAndSend
                {
                    FailedMessageId = failedMessageId,
                    // Encode the body in base64 so that the new body doesn't have to be escaped
                    NewBody = base64String,
                }).ConfigureAwait(false);



                return HttpStatusCode.Accepted;
            };
        }

        public IMessageSession Bus { get; set; }
    }
}