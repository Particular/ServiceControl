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

            Post["/edit/{failedmessageid}", true] = async (parameters, token) =>
            {
                if (!Settings.AllowMessageEditing)
                {
                    return HttpStatusCode.NotFound;
                }

                string failedMessageId = parameters.FailedMessageId;

                if (string.IsNullOrEmpty(failedMessageId))
                {
                    return HttpStatusCode.BadRequest;
                }

                var edit = this.Bind<EditMessageModel>();

                FailedMessage failedMessage;
                var originalMessageId = edit.MessageHeaders.First(x => string.Compare(x.Key, "NServiceBus.MessageId", StringComparison.InvariantCulture) == 0);

                using (var session = Store.OpenAsyncSession())
                {
                    failedMessage = await session.LoadAsync<FailedMessage>(failedMessageId).ConfigureAwait(false);
                }

                //WARN
                /*
                 * failedMessage.ProcessingAttempts.Last() return the lat retry attempt.
                 * In theory between teh time someone edits a failed message and retry it someone else
                 * could have retried the same message without editing. If this is the case "Last()" is
                 * not anymore the same message.
                 * Instead of using Last() it's probably better to select the processing attempt by looking for
                 * one with the same MessageID
                 */

                if (LockedHeaderModificationValidator.Check(GetEditConfiguration().LockedHeaders, edit.MessageHeaders.ToList(), failedMessage.ProcessingAttempts.Last().Headers))
                {
                    //TODO: log that edited locked headers were found?
                    return HttpStatusCode.BadRequest;
                }
<<<<<<< HEAD
=======

                //TODO: should we verify here if the edit body is still a valid xml or json?
>>>>>>> use failed message id, and warming comment

                if (string.IsNullOrWhiteSpace(edit.MessageBody) || edit.MessageHeaders == null)
                {
                    return HttpStatusCode.BadRequest;
                }

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
        public string MessageBody { get; set; }

        // this way dictionary keys won't be converted to properties and renamed due to the UnderscoreMappingResolver
        public IEnumerable<KeyValuePair<string, string>> MessageHeaders { get; set; }
    }
}