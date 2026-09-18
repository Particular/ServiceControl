namespace ServiceControl.Contracts.Operations
{
    using System;

    public class FailureDetails
    {
        public FailureDetails()
        {
#pragma warning disable RS0030 // Do not use banned apis: default field value does not need to use external time provider
            TimeOfFailure = DateTime.UtcNow;
#pragma warning restore RS0030
        }

        public required string AddressOfFailingEndpoint { get; set; }

        public DateTime TimeOfFailure { get; set; }

        public ExceptionDetails? Exception { get; set; }
    }
}