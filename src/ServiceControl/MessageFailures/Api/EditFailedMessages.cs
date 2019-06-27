namespace ServiceControl.MessageFailures.Api
{
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    class EditFailedMessages : BaseModule
    {
        public EditFailedMessages()
        {
            Get["/edit/config"] = _ => Negotiate.WithModel(GetEditConfiguration());

            Post["/edit/{messageid}", true] = async (parameters, token) =>
            {
                if (!Settings.AllowMessageEditing)
                {
                    return HttpStatusCode.NotFound;
                }

                string failedMessageId = parameters.MessageId;

                if (string.IsNullOrEmpty(failedMessageId))
                {
                    return HttpStatusCode.BadRequest;
                }

                var edit = this.Bind<EditMessageModel>();

                FailedMessage originalMessage;
                var originalMessageId = edit.MessageHeaders.First(x => string.Compare(x.Key, "NServiceBus.MessageId", StringComparison.InvariantCulture) == 0);
                
                using (var session = Store.OpenAsyncSession())
                {
                    originalMessage = await session.LoadAsync<FailedMessage>(originalMessageId).ConfigureAwait(false);
                }

                if (LockedHeaderModificationValidator.Check(GetEditConfiguration().LockedHeaders, edit.MessageHeaders.ToList(), originalMessage.ProcessingAttempts.Last().Headers))
                {
                    //TODO: log that edited locked headers were found?
                    return HttpStatusCode.BadRequest;
                }
                
                //TODO: should we verify here if the edit body is still a valid xml or json?

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

        public LockedHeaderModificationValidator LockedHeaderModificationValidator { get; set; }

        EditConfigurationModel GetEditConfiguration()
        {
            return new EditConfigurationModel
            {
                Enabled = Settings.AllowMessageEditing,
                LockedHeaders = new[]
                {
                    "NServiceBus.MessageId",
                    "NServiceBus.ExceptionInfo.ExceptionType",
                    "NServiceBus.ExceptionInfo.HelpLink",
                    "NServiceBus.ExceptionInfo.Message",
                    "NServiceBus.ExceptionInfo.Source",
                    "NServiceBus.ExceptionInfo.StackTrace"
                },
                SensitiveHeaders = new[]
                {
                    "NServiceBus.ConversationId",
                    "NServiceBus.MessageIntent"
                }
            };
        }

        public IMessageSession Bus { get; set; }
    }

    class EditConfigurationModel
    {
        public bool Enabled { get; set; }
        public string[] SensitiveHeaders { get; set; }
        public string[] LockedHeaders { get; set; }
    }

    class EditMessageModel
    {
        public string MessageBody{ get; set; }

        // this way dictionary keys won't be converted to properties and renamed due to the UnderscoreMappingResolver
        public IEnumerable<KeyValuePair<string, string>> MessageHeaders { get; set; }
    }
}