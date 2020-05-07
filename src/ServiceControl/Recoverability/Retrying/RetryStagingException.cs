namespace ServiceControl.Recoverability
{
    using System;

    class RetryStagingException : Exception
    {
        public RetryStagingException(Exception innerException) : base("Staging failed. Retry required", innerException)
        {
        }
    }
}