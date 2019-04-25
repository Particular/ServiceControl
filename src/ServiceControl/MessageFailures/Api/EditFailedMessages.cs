namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Nancy;
    using Nancy.ModelBinding;
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

                var edit = this.Bind<EditMessageModel>();

                if (edit == null || string.IsNullOrWhiteSpace(edit.MessageBody) || edit.MessageHeaders == null)
                {
                    //TODO: load original body if no new body provided?
                    //TODO: load original headers if no new headers provided?
                    return HttpStatusCode.BadRequest;
                }

                //TODO: consider sending base64 encoded body from the client
                // Encode the body in base64 so that the new body doesn't have to be escaped
                var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(edit.MessageBody));
                await Bus.SendLocal(new EditAndSend
                {
                    FailedMessageId = failedMessageId,
                    NewBody = base64String,
                    NewHeaders = edit.MessageHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                }).ConfigureAwait(false);

                return HttpStatusCode.Accepted;
            };
        }

        public IMessageSession Bus { get; set; }
    }

    class EditMessageModel
    {
        public string MessageBody { get; set; }

        // this way dictionary keys won't be converted to properties and renamed due to the UnderscoreMappingResolver
        public IEnumerable<KeyValuePair<string, string>> MessageHeaders { get; set; }
    }
}