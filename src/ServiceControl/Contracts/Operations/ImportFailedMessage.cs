namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Faults;
    using Infrastructure;

    public class ImportFailedMessage : ImportMessage
    {
        public ImportFailedMessage(TransportMessage message)
            : base(message)
        {
            FailureDetails = ParseFailureDetails(message.Headers);
            FailingEndpointId = Address.Parse(FailureDetails.AddressOfFailingEndpoint).Queue;
        }


        public string FailingEndpointId { get; set; }

        public FailureDetails FailureDetails { get; set; }


        FailureDetails ParseFailureDetails(Dictionary<string, string> headers)
        {
            var result = new FailureDetails();

            DictionaryExtensions.CheckIfKeyExists("NServiceBus.TimeOfFailure", headers, s => result.TimeOfFailure = DateTimeExtensions.ToUtcDateTime(s));

            result.Exception = GetException(headers);

            result.AddressOfFailingEndpoint = headers[FaultsHeaderKeys.FailedQ];

            return result;
        }

      
        ExceptionDetails GetException(IDictionary<string, string> headers)
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


    }
}
