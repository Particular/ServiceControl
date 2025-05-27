namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Contracts.Operations;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Faults;
    using NServiceBus.Transport;
    using Recoverability;
    using FailedMessage = MessageFailures.FailedMessage;

    class FailedMessageFactory
    {
        public FailedMessageFactory(IFailedMessageEnricher[] failedEnrichers)
        {
            this.failedEnrichers = failedEnrichers;
        }

        public List<FailedMessage.FailureGroup> GetGroups(string messageType, FailureDetails failureDetails, FailedMessage.ProcessingAttempt processingAttempt)
        {
            var groups = new List<FailedMessage.FailureGroup>();

            foreach (var enricher in failedEnrichers)
            {
                groups.AddRange(enricher.Enrich(messageType, failureDetails, processingAttempt));
            }

            return groups;
        }

        public FailureDetails ParseFailureDetails(MessageContext context)
        {
            var result = new FailureDetails();

            DictionaryExtensions.CheckIfKeyExists("NServiceBus.TimeOfFailure", context.Headers, s => result.TimeOfFailure = DateTimeOffsetHelper.ToDateTimeOffset(s).UtcDateTime);

            result.Exception = GetException(context.Headers);

            var returnQueueResolver = context.Extensions.Get<ReturnQueueResolver>("ReturnQueueName");

            try
            {
                var returnQueueName = returnQueueResolver.Resolve(context);

                if (string.IsNullOrEmpty(returnQueueName))
                {
                    throw new Exception($"The calculated return queue name for error queue '{context.ReceiveAddress}' by resolver '{returnQueueResolver.ResolverName}' is null or empty.");
                }

                result.AddressOfFailingEndpoint = returnQueueName;
                result.AcknowledgementQueue = context.ReceiveAddress;
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to determine the return queue address from message with native id {context.NativeMessageId} received from queue {context.ReceiveAddress}.", ex);
            }            

            return result;
        }

        static ExceptionDetails GetException(IReadOnlyDictionary<string, string> headers)
        {
            var exceptionDetails = new ExceptionDetails();
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.ExceptionType", headers,
                s => exceptionDetails.ExceptionType = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.Message", headers,
                s => exceptionDetails.Message = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.Source", headers,
                s => exceptionDetails.Source = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.StackTrace", headers,
                s => exceptionDetails.StackTrace = s);
            return exceptionDetails;
        }

        public FailedMessage.ProcessingAttempt CreateProcessingAttempt(Dictionary<string, string> headers, Dictionary<string, object> metadata, FailureDetails failureDetails)
        {
            return new FailedMessage.ProcessingAttempt
            {
                AttemptedAt = failureDetails.TimeOfFailure,
                FailureDetails = failureDetails,
                MessageMetadata = metadata,
                MessageId = headers[Headers.MessageId],
                Headers = headers
            };
        }

        IFailedMessageEnricher[] failedEnrichers;
    }
}