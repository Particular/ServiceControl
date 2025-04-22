namespace ServiceControl.Contracts.Operations
{
    using System;

    public class FailureDetails
    {
        public string AddressOfFailingEndpoint { get; set; }

        public DateTime TimeOfFailure { get; set; } = DateTime.UtcNow;

        public ExceptionDetails Exception { get; set; }
    }
}