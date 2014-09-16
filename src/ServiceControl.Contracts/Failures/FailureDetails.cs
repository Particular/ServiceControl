namespace ServiceControl.Contracts.Failures
{
    using System;

    public class FailureDetails
    {
        public string AddressOfFailingEndpoint { get; set; }
        public DateTime TimeOfFailure { get; set; }
        public ExceptionDetails Exception { get; set; }

    }
}