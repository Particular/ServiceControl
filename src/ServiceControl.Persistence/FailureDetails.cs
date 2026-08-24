namespace ServiceControl.Contracts.Operations
{
    using System;

    public class FailureDetails
    {
        public FailureDetails()
        {
            TimeOfFailure = DateTime.UtcNow;
        }

        public string AddressOfFailingEndpoint { get; set; } = string.Empty;

        public DateTime TimeOfFailure { get; set; }

        public ExceptionDetails? Exception { get; set; }
    }
}