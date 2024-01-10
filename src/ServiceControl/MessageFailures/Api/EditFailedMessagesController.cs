namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using NServiceBus.Logging;
    using Persistence;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api")]
    public class EditFailedMessagesController(
        Settings settings,
        IErrorMessageDataStore store,
        IMessageSession session)
        : ControllerBase
    {
        [Route("edit/config")]
        [HttpGet]
        public EditConfigurationModel Config() => GetEditConfiguration();

        [Route("edit/{failedmessageid}")]
        [HttpPost]
        public async Task<IActionResult> Edit(string failedMessageId, EditMessageModel edit)
        {
            if (!settings.AllowMessageEditing)
            {
                logging.Info("Message edit-retry has not been enabled.");
                return NotFound();
            }

            if (string.IsNullOrEmpty(failedMessageId))
            {
                return BadRequest();
            }

            var failedMessage = await store.ErrorBy(failedMessageId);

            if (failedMessage == null)
            {
                logging.WarnFormat("The original failed message could not be loaded for id={0}", failedMessageId);
                return BadRequest();
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
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(edit.MessageBody) || edit.MessageHeaders == null)
            {
                logging.WarnFormat("There is no message body on the edit-retry for MessageID {0}.", failedMessageId);
                return BadRequest();
            }

            // Encode the body in base64 so that the new body doesn't have to be escaped
            var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(edit.MessageBody));
            await session.SendLocal(new EditAndSend
            {
                FailedMessageId = failedMessageId,
                NewBody = base64String,
                NewHeaders = edit.MessageHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            });

            return Accepted();
        }


        EditConfigurationModel GetEditConfiguration() =>
            new()
            {
                Enabled = settings.AllowMessageEditing,
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

        static readonly ILog logging = LogManager.GetLogger(typeof(EditFailedMessagesController));
    }

    public class EditConfigurationModel
    {
        public bool Enabled { get; set; }
        public string[] SensitiveHeaders { get; set; }
        public string[] LockedHeaders { get; set; }
    }

    public class EditMessageModel
    {
        public string MessageBody { get; set; }

        // this way dictionary keys won't be converted to properties and renamed due to the UnderscoreMappingResolver
        public IEnumerable<KeyValuePair<string, string>> MessageHeaders { get; set; }
    }
}