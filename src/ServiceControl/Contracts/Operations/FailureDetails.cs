namespace ServiceControl.Contracts.Operations
{
    using System;
    using System.Collections.Generic;
    using MessageAuditing;
    using NServiceBus;
    using NServiceBus.Faults;

    public class FailureDetails
    {
        public FailureDetails()
        {
        }

        public FailureDetails(IDictionary<string,string> headers)
        {
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.TimeOfFailure", headers, s => TimeOfFailure = DateTimeExtensions.ToUtcDateTime(s));
            Exception = GetException(headers);

            AddressOfFailingEndpoint = Address.Parse(headers[FaultsHeaderKeys.FailedQ]);
        }

        
      
        public Address AddressOfFailingEndpoint { get; set; }


        public DateTime TimeOfFailure { get; set; }

        public ExceptionDetails Exception { get; set; }

        ExceptionDetails GetException(IDictionary<string,string> headers)
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