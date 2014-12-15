namespace ServiceControl.Contracts.Operations
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Faults;

    public class ImportFailedMessage : ImportMessage
    {
        public ImportFailedMessage(TransportMessage message, string uniqueId)
            : base(message, uniqueId)
        {
            FailureDetails = ParseFailureDetails(message.Headers);
            FailingEndpointId = Address.Parse(FailureDetails.AddressOfFailingEndpoint).Queue;
        }


        public string FailingEndpointId { get; set; }

        public FailureDetails FailureDetails { get; set; }


        FailureDetails ParseFailureDetails(Dictionary<string, string> headers)
        {
            var result = new FailureDetails();

            CheckIfKeyExists("NServiceBus.TimeOfFailure", headers, s => result.TimeOfFailure = DateTimeExtensions.ToUtcDateTime(s));

            result.Exception = GetException(headers);

            result.AddressOfFailingEndpoint = headers[FaultsHeaderKeys.FailedQ];

            return result;
        }

      
        ExceptionDetails GetException(IDictionary<string, string> headers)
        {
            var exceptionDetails = new ExceptionDetails();
            CheckIfKeyExists("NServiceBus.ExceptionInfo.ExceptionType", headers,
                s => exceptionDetails.ExceptionType = s);
            CheckIfKeyExists("NServiceBus.ExceptionInfo.Message", headers,
                s => exceptionDetails.Message = s);
            CheckIfKeyExists("NServiceBus.ExceptionInfo.Source", headers,
                s => exceptionDetails.Source = s);
            CheckIfKeyExists("NServiceBus.ExceptionInfo.StackTrace", headers,
                s => exceptionDetails.StackTrace = s);
            return exceptionDetails;
        }

        public static void CheckIfKeyExists(string key, IDictionary<string, string> headers, Action<string> actionToInvokeWhenKeyIsFound)
        {
            string value;
            if (headers.TryGetValue(key, out value))
            {
                actionToInvokeWhenKeyIsFound(value);
            }
        }

    }
}
