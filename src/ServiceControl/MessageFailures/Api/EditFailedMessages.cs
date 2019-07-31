namespace ServiceControl.MessageFailures.Api
{
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using NServiceBus.Logging;
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
                    logging.Info("Message edit-retry has not been enabled.");
                    return HttpStatusCode.NotFound;
                }

                string failedMessageId = parameters.FailedMessageId;

                if (string.IsNullOrEmpty(failedMessageId))
                {
                    return HttpStatusCode.BadRequest;
                }

                var edit = this.Bind<EditMessageModel>();

                FailedMessage failedMessage;

                using (var session = Store.OpenAsyncSession())
                {
                    failedMessage = await session.LoadAsync<FailedMessage>(FailedMessage.MakeDocumentId(failedMessageId)).ConfigureAwait(false);
                }

                if (failedMessage == null)
                {
                    logging.WarnFormat("The original failed message could not be loaded for id={0}", failedMessageId);
                    return HttpStatusCode.BadRequest;
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

                if (LockedHeaderModificationValidator.Check(GetEditConfiguration().LockedHeaders, edit.MessageHeaders.ToDictionary(x => x.Key, x => x.Value), failedMessage.ProcessingAttempts.Last().Headers))
                {
                    logging.WarnFormat("Locked headers have been modified on the edit-retry for MessageID {0}.", failedMessageId);
                    return HttpStatusCode.BadRequest;
                }

                if (string.IsNullOrWhiteSpace(edit.MessageBody) || edit.MessageHeaders == null)
                {
                    logging.WarnFormat("There is no message body on the edit-retry for MessageID {0}.", failedMessageId);
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
                    Headers.MessageId,
                    Headers.SagaId,
                    Headers.CorrelationId,
                    Headers.ControlMessageHeader,
                    Headers.OriginatingSagaId,
                    Headers.RelatedTo,
                    Headers.ConversationId,
                    Headers.MessageIntent,
                    Headers.NServiceBusVersion,
                    Headers.IsSagaTimeoutMessage,
                    Headers.IsDeferredMessage,
                    Headers.DelayedRetries,
                    Headers.DelayedRetriesTimestamp,
                    Headers.ImmediateRetries,
                    Headers.ProcessingStarted,
                    Headers.ProcessingEnded,
                    "NServiceBus.ExceptionInfo.ExceptionType",
                    "NServiceBus.ExceptionInfo.HelpLink",
                    "NServiceBus.ExceptionInfo.Message",
                    "NServiceBus.ExceptionInfo.Source",
                    "NServiceBus.ExceptionInfo.StackTrace",
                    "NServiceBus.TimeOfFailure",
                    "NServiceBus.FailedQ"
                },
                SensitiveHeaders = new[]
                {
                    Headers.RouteTo,
                    Headers.DestinationSites,
                    Headers.OriginatingSite,
                    Headers.HttpTo,
                    Headers.ReplyToAddress,
                    Headers.ReturnMessageErrorCodeHeader,
                    Headers.SagaType,
                    Headers.OriginatingSagaType,
                    Headers.TimeSent,
                    "Header"
                }
            };
        }

        public IMessageSession Bus { get; set; }

        ILog logging = LogManager.GetLogger(typeof(EditFailedMessages));
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