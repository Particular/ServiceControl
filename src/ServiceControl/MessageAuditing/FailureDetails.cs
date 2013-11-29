namespace ServiceBus.Management.MessageAuditing
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;

    public class FailureDetails
    {
        public FailureDetails()
        {
        }

        public FailureDetails(IDictionary<string,string> headers)
        {
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.FailedQ", headers, s => FailedInQueue = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.TimeOfFailure", headers, s => TimeOfFailure = DateTimeExtensions.ToUtcDateTime(s));
            Exception = GetException(headers);
            NumberOfTimesFailed = 1;
        }

        public int NumberOfTimesFailed { get; set; }

        public string FailedInQueue { get; set; }

        public DateTime TimeOfFailure { get; set; }

        public ExceptionDetails Exception { get; set; }

        public DateTime ResolvedAt { get; set; }

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

        public void RegisterException(IDictionary<string,string> headers)
        {
            NumberOfTimesFailed++;

            var timeOfFailure = DateTime.MinValue;

            DictionaryExtensions.CheckIfKeyExists("NServiceBus.TimeOfFailure", headers, s => timeOfFailure = DateTimeExtensions.ToUtcDateTime(s));

            if (TimeOfFailure < timeOfFailure)
            {
                Exception = GetException(headers);
                TimeOfFailure = timeOfFailure;
            }

            //todo -  add history
        }
    }
}