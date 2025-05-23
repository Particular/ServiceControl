namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Contracts.Operations;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Faults;
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

        public FailureDetails ParseFailureDetails(IReadOnlyDictionary<string, string> headers, Dictionary<string, object> metadata)
        {
            var result = new FailureDetails();

            DictionaryExtensions.CheckIfKeyExists("NServiceBus.TimeOfFailure", headers, s => result.TimeOfFailure = DateTimeOffsetHelper.ToDateTimeOffset(s).UtcDateTime);

            result.Exception = GetException(headers);

            var hasFailedQHeader = headers.ContainsKey(FaultsHeaderKeys.FailedQ);
            var hasFailedQHeaderValue = headers.TryGetValue(FaultsHeaderKeys.FailedQ, out var failedQHeaderValue) && !string.IsNullOrEmpty(failedQHeaderValue);
            var hasReturnToQueueHeader = metadata.ContainsKey("ReturnToQueue");
            var hasReturnToQueueHeaderValue = metadata.TryGetValue("ReturnToQueue", out var returnToQueueHeaderValue) && returnToQueueHeaderValue is string && !string.IsNullOrEmpty(returnToQueueHeaderValue as string);

            if ((!hasFailedQHeader && !hasReturnToQueueHeader) || (!hasFailedQHeaderValue && !hasReturnToQueueHeaderValue))
            {
                var sb = new StringBuilder();

                sb.Append("Could not determine the address of the failing endpoint. ");

                if (!hasFailedQHeader && !hasReturnToQueueHeader))
                {
                    sb.Append($"Could not find an {FaultsHeaderKeys.FailedQ}" header or could not determine the return queue from the metadata.");

                    throw new Exception($"Missing '{FaultsHeaderKeys.FailedQ}' header. Message is poison message or incorrectly send to (error) queue.");
                }
            }

            result.AddressOfFailingEndpoint = headers.ContainsKey(FaultsHeaderKeys.FailedQ) ? headers[FaultsHeaderKeys.FailedQ] : metadata["ReturnToQueue"].ToString();

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