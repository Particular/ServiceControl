namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using Persistence;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api")]
    public class EditFailedMessagesController(
        Settings settings,
        IErrorMessageDataStore store,
        IMessageSession session,
        ILogger<EditFailedMessagesController> logger)
        : ControllerBase
    {
        [Route("edit/config")]
        [HttpGet]
        public EditConfigurationModel Config() => GetEditConfiguration();

        [Route("edit/{failedMessageId:required:minlength(1)}")]
        [HttpPost]
        public async Task<IActionResult> Edit(string failedMessageId, [FromBody] EditMessageModel edit)
        {
            if (!settings.AllowMessageEditing)
            {
                logger.LogInformation("Message edit-retry has not been enabled");
                return NotFound();
            }

            //HINT: This validation is the first one because we want to minimize the chance of two users concurrently execute an edit-retry.
            var editManager = await store.CreateEditFailedMessageManager();
            var editId = await editManager.GetCurrentEditingMessageId(failedMessageId);
            if (editId != null)
            {
                logger.LogWarning("Cannot edit message {FailedMessageId} because it has already been edited", failedMessageId);
                // We return HTTP 200 even though the edit is being ignored. This is to keep the compatibility with older versions of ServicePulse.
                // Newer ServicePulse versions (starting with x.x) will handle the response's payload accordingly.
                return Ok(new { EditIgnored = true });
            }

            var failedMessage = await store.ErrorBy(failedMessageId);

            if (failedMessage == null)
            {
                logger.LogWarning("The original failed message could not be loaded for id={FailedMessageId}", failedMessageId);
                return BadRequest();
            }

            //WARN
            /*
             * failedMessage.ProcessingAttempts.Last() return the last retry attempt.
             * In theory between the time someone edits a failed message and retry it someone else
             * could have retried the same message without editing. If this is the case "Last()" is
             * not anymore the same message.
             * Instead of using Last() it's probably better to select the processing attempt by looking for
             * one with the same MessageID
             */

            if (LockedHeaderModificationValidator.Check(GetEditConfiguration().LockedHeaders, edit.MessageHeaders, failedMessage.ProcessingAttempts.Last().Headers))
            {
                logger.LogWarning("Locked headers have been modified on the edit-retry for MessageID {FailedMessageId}", failedMessageId);
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(edit.MessageBody) || edit.MessageHeaders == null)
            {
                logger.LogWarning("There is no message body on the edit-retry for MessageID {FailedMessageId}", failedMessageId);
                return BadRequest();
            }

            // Encode the body in base64 so that the new body doesn't have to be escaped
            var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(edit.MessageBody));
            await session.SendLocal(new EditAndSend
            {
                FailedMessageId = failedMessageId,
                NewBody = base64String,
                NewHeaders = edit.MessageHeaders
            });

            return Accepted(new { EditIgnored = false });
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

        public Dictionary<string, string> MessageHeaders { get; set; }
    }
}